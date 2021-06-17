﻿using System;
using System.Threading.Tasks;
using Bit.Core.Models.Table;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bit.Portal.Utilities
{
    public class EnterprisePortalTokenSignInManager : SignInManager<User>
    {
        public const string TokenSignInPurpose = "EnterprisePortalTokenSignIn";
        
        private readonly IDataProtector _dataProtector;

        public EnterprisePortalTokenSignInManager(
            UserManager<User> userManager,
            IHttpContextAccessor contextAccessor,
            IUserClaimsPrincipalFactory<User> claimsFactory,
            IOptions<IdentityOptions> optionsAccessor,
            ILogger<SignInManager<User>> logger,
            IAuthenticationSchemeProvider schemes,
            IUserConfirmation<User> confirmation,
            IDataProtectionProvider dataProtectionProvider)
            : base(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
        {
            _dataProtector = dataProtectionProvider.CreateProtector(TokenSignInPurpose);
        }

        public async Task<SignInResult> TokenSignInAsync(User user, string token, bool isPersistent)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            var attempt = await CheckTokenSignInAsync(user, token);
            return attempt.Succeeded ?
                await SignInOrTwoFactorAsync(user, isPersistent, bypassTwoFactor: true) : attempt;
        }

        public async Task<SignInResult> TokenSignInAsync(string userId, string token, bool isPersistent)
        {
            var user = await UserManager.FindByIdAsync(userId);
            if (user == null)
            {
                return SignInResult.Failed;
            }

            return await TokenSignInAsync(user, token, isPersistent);
        }

        public virtual async Task<SignInResult> CheckTokenSignInAsync(User user, string token)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            var error = await PreSignInCheck(user);
            if (error != null)
            {
                return error;
            }
            // CfDJ8DRbSGPbyppMqeryzDEiQtvpaJ5m3Y9YHufqaOJ7JAdZpsLWXW+iJqKH4kuv522tbxipy9rzkwStV6qxXeWurYgoO/lSLjZImpjN3xBT6ITvOOOptUAOUQGBCLkDQqewRflGzKmqDZdrxcqusTkypLomM8w0Bxylfx2+O79dk3eprhder86Cul3ah5tA+p3oSmCCZjYZ4Kg0HHbGC/Z7zPdZxmtlA55nLGPKfTnOhQWoC6K6TVzNwuY3HCiUaTgQ1A==

            Logger.LogInformation("CheckTokenSignInAsync: token='{token}', user='{userId}'",
                token, await UserManager.GetUserIdAsync(user));

/*
            var unprotectedToken = _dataProtector.Unprotect(token);
            Logger.LogInformation("CheckTokenSignInAsync: token='{token}', user='{userId}', unprotectedToken='{unprotectedToken}'",
                token, await UserManager.GetUserIdAsync(user), unprotectedToken);
*/

            if (await UserManager.VerifyUserTokenAsync(user, Options.Tokens.PasswordResetTokenProvider,
                TokenSignInPurpose, token))
            {
                return SignInResult.Success;
            }

            Logger.LogWarning(2, "User {userId} failed to provide the correct enterprise portal token.",
                await UserManager.GetUserIdAsync(user));
            return SignInResult.Failed;
        }
    }
}
