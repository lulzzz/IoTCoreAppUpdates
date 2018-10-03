﻿using Hyprsoft.IoT.AppUpdates.Web.Areas.AppUpdates.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Hyprsoft.IoT.AppUpdates.Web.Areas.AppUpdates.Controllers
{
    public class AccountController : BaseController
    {
        #region Fields

        private readonly IConfiguration _configuration;

        #endregion

        #region Constructors

        public AccountController(UpdateManager manager, IConfiguration configuration) : base(manager)
        {
            _configuration = configuration;
        }

        #endregion

        #region Methods

        public IActionResult Login(string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        public IActionResult AccessDenied()
        {
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(Login model, string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (ModelState.IsValid)
            {
                var credentialProvider = await new CredentialProviderHelper(_configuration).CreateProviderAsync();
                var username = await credentialProvider.GetUsernameAsync();
                if (String.Compare(model.Username, username, true) == 0 && model.Password == await credentialProvider.GetPasswordAsync())
                {
                    var claims = new List<Claim> { new Claim(ClaimTypes.Name, username) };
                    var authenticationProperties = new AuthenticationProperties
                    {
                        AllowRefresh = true,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddDays(AuthenticationSettings.CookieExpirationDays),
                        IsPersistent = false,
                        IssuedUtc = DateTime.UtcNow
                    };
                    await HttpContext.SignInAsync(AuthenticationSettings.CookieAuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, AuthenticationSettings.CookieAuthenticationScheme)), authenticationProperties);
                    if (Url.IsLocalUrl(returnUrl))
                        return Redirect(returnUrl);
                    else
                        return RedirectToAction("List", "Apps", new { Area = "AppUpdates" });
                }
                else
                    TempData["Error"] = "Invalid login attempt.  Please try again.";
            }   // model state valid?
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(AuthenticationSettings.CookieAuthenticationScheme);
            return RedirectToAction("List", "Apps", new { Area = "AppUpdates" });
        }

        [HttpPost]
        public IActionResult Token([FromBody] Login model)
        {
            if (String.Compare(BearerAuthenticationSettings.DefaultUsername, model.Username, true) != 0 || model.Password != BearerAuthenticationSettings.DefaultPassword)
                return Unauthorized();

            var claims = new List<Claim> { new Claim(ClaimTypes.Name, model.Username) };
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(AuthenticationSettings.DefaultBearerSecurityKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer: AuthenticationSettings.DefaultBearerIssuer,
                audience: AuthenticationSettings.DefaultBearerAudience,
                claims: claims,
                expires: DateTime.Now.AddDays(AuthenticationSettings.BearerTokenExpirationDays),
                signingCredentials: credentials);

            return Ok(new { token = new JwtSecurityTokenHandler().WriteToken(token) });
        }

        #endregion
    }
}
