﻿using System.Web.Mvc;
using MrCMS.Entities.People;
using MrCMS.Helpers;
using MrCMS.Web.Apps.Core.Models;
using MrCMS.Web.Apps.Core.Pages;
using MrCMS.Website;
using MrCMS.Website.Controllers;
using MrCMS.Services;

namespace MrCMS.Web.Apps.Core.Controllers
{
    public class UserAccountController : MrCMSAppUIController<CoreApp>
    {
        private readonly IUserService _userService;
        private readonly IAuthorisationService _authorisationService;

        public UserAccountController(IUserService userService, IAuthorisationService authorisationService)
        {
            _userService = userService;
            _authorisationService = authorisationService;
        }

        public ActionResult Show(UserAccountPage page, int pageNum = 1)
        {
            if (CurrentRequestData.CurrentUser != null)
            {
                ViewBag.PageNum = pageNum;
                return View(page);
            }
            return Redirect(UniquePageHelper.GetUrl<LoginPage>());
        }

        [HttpGet]
        public ActionResult UserAccountDetails(UserAccountModel model)
        {
            User user = CurrentRequestData.CurrentUser;
            if (user != null)
            {
                model.FirstName = user.FirstName;
                model.LastName = user.LastName;
                model.Email = user.Email;
                ModelState.Clear();
                return View(model);
            }
            return Redirect(UniquePageHelper.GetUrl<LoginPage>());
        }

        [HttpPost]
        [ActionName("UserAccountDetails")]
        public ActionResult UserAccountDetails_POST(UserAccountModel model)
        {
            if (model != null && ModelState.IsValid)
            {
                var user = CurrentRequestData.CurrentUser;
                if (user != null && user.IsActive)
                {
                    user.FirstName = model.FirstName;
                    user.LastName = model.LastName;
                    user.Email = model.Email;
                    _userService.SaveUser(user);
                    _authorisationService.SetAuthCookie(user.Email, false);

                    return Redirect(UniquePageHelper.GetUrl<UserAccountPage>());
                }
            }
            return Redirect(UniquePageHelper.GetUrl<UserAccountPage>());
        }

        public JsonResult IsUniqueEmail(string email)
        {
            if (_userService.IsUniqueEmail(email, CurrentRequestData.CurrentUser.Id))
                return Json(true, JsonRequestBehavior.AllowGet);

            return Json("Email already registered.", JsonRequestBehavior.AllowGet);
        }
    }
}