using EPiServer.Cms.UI.AspNetIdentity;
using EPiServer.ServiceLocation;
using Mediachase.BusinessFoundation;
using Mediachase.BusinessFoundation.Data;
using Mediachase.Commerce.Customers;
using Microsoft.AspNet.Identity;
using System;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace StefanOlsen.Manager.Apps.Customer.Modules
{
    public partial class IdentityAccountChangePassword : UserControl
    {
        private readonly MapUserKey _mapUserKey;
        private readonly ApplicationUserManager<ApplicationUser> _userManager;
        private readonly ResourceManager _resourceManagerProfile;
        private readonly ResourceManager _resourceManagerShared;

        public IdentityAccountChangePassword()
            : this(
                ServiceLocator.Current.GetInstance<MapUserKey>(),
                ServiceLocator.Current.GetInstance<ApplicationUserManager<ApplicationUser>>())
        {
        }

        public IdentityAccountChangePassword(
            MapUserKey mapUserKey,
            ApplicationUserManager<ApplicationUser> userManager)
        {
            _mapUserKey = mapUserKey;
            _userManager = userManager;
            _resourceManagerProfile = new ResourceManager("Resources.ProfileStrings", Assembly.Load("App_GlobalResources"));
            _resourceManagerShared = new ResourceManager("Resources.SharedStrings", Assembly.Load("App_GlobalResources"));
        }

        protected PrimaryKeyId ContactId
        {
            get
            {
                PrimaryKeyId primaryKeyId = Request.QueryString["ContactId"] != null
                    ? PrimaryKeyId.Parse(Request.QueryString["ContactId"])
                    : PrimaryKeyId.Empty;

                return primaryKeyId;
            }
        }

        protected string CommandName => Request.QueryString["commandName"] ?? string.Empty;
        protected string UserName => Request.QueryString["UserName"];

        protected void Page_Load(object sender, EventArgs e)
        {
            lblErrorInfo.Text = string.Empty;
            if (!IsPostBack)
            {
                TrChangePassword.Visible = false;
                TrChangePasswordForced.Visible = true;
                TbUserName.Text = GetUser().UserName;
                TbUserName.Enabled = false;

                PasswordValidator passwordValidator = _userManager.PasswordValidator as PasswordValidator;
                PasswordChangeDescription.Text = string.Format(
                    _resourceManagerProfile.GetString("Password_Change_Description") ?? throw new InvalidOperationException(),
                    passwordValidator?.RequiredLength);
            }

            PasswordCustomValidator.Enabled = true;

            BindButtons();
        }

        private void BindButtons()
        {
            btnSave.Text = GetGlobalResourceObject("Common", "btnOK").ToString();
            btnSave.CustomImage = Page.ResolveUrl("~/Apps/MetaDataBase/images/ok-button.gif");
            btnCancel.Text = GetGlobalResourceObject("Common", "btnCancel").ToString();
            btnCancel.CustomImage = Page.ResolveUrl("~/Apps/MetaDataBase/images/cancel-button.gif");
            btnCancel.Attributes.Add("onclick", CommandHandler.GetCloseOpenedFrameScript(Page, string.Empty, refreshParent: false, addReturn: true));

            Button button = (Button)aspChangePassword.ChangePasswordTemplateContainer.FindControl("btnRealChangePassword");
            button.Style.Add(HtmlTextWriterStyle.Display, "none");

            IMButton iMButton = (IMButton)aspChangePassword.ChangePasswordTemplateContainer.FindControl("ChangePasswordPushButton");
            iMButton.Text = GetGlobalResourceObject("Common", "btnOK").ToString();
            iMButton.CustomImage = Page.ResolveUrl("~/Apps/MetaDataBase/images/ok-button.gif");
            iMButton.Attributes.Add("onclick", $"{Page.ClientScript.GetPostBackEventReference(button, string.Empty)};return false;");

            IMButton iMButton2 = (IMButton)aspChangePassword.ChangePasswordTemplateContainer.FindControl("CancelPushButton");
            iMButton2.Text = GetGlobalResourceObject("Common", "btnCancel").ToString();
            iMButton2.CustomImage = Page.ResolveUrl("~/Apps/MetaDataBase/images/cancel-button.gif");
            iMButton2.Attributes.Add("onclick", CommandHandler.GetCloseOpenedFrameScript(Page, string.Empty, refreshParent: false, addReturn: true));

            aspChangePassword.ChangedPassword += aspChangePassword_ChangedPassword;
        }

        private void aspChangePassword_ChangedPassword(object sender, EventArgs e)
        {
            ClosePopup();
        }

        public void UserPassword_ServerValidate(object source, ServerValidateEventArgs args)
        {
            args.IsValid = false;
            string password = TbNewPassword.Text.Trim();

            IdentityResult result = _userManager.PasswordValidator.ValidateAsync(password)
                .GetAwaiter()
                .GetResult();
            if (result.Succeeded)
            {
                args.IsValid = true;
                return;
            }

            PasswordCustomValidator.ErrorMessage = _resourceManagerShared.GetString("Password_Strong_ValidationError");
        }

        protected void btnSave_ServerClick(object sender, EventArgs e)
        {
            Page.Validate();
            if (!Page.IsValid)
            {
                return;
            }

            ApplicationUser user = GetUser();
            if (user == null)
            {
                ClosePopup();
                return;
            }

            string text = TbNewPassword.Text.Trim();
            if (!string.IsNullOrEmpty(text))
            {
                string token = _userManager.GeneratePasswordResetToken(user.Id);
                IdentityResult result = _userManager.ResetPassword(user.Id, token, text);
                if (!result.Succeeded)
                {
                    lblErrorInfo.Visible = true;
                    lblErrorInfo.Text = result.Errors.FirstOrDefault();
                    return;
                }
            }

            ClosePopup();
        }

        private void ClosePopup()
        {
            string sParams = string.Empty;
            if (!string.IsNullOrEmpty(CommandName))
            {
                CommandParameters commandParameters = new CommandParameters(CommandName);
                sParams = commandParameters.ToString();
            }

            CommandHandler.RegisterCloseOpenedFrameScript(Page, sParams, refreshParent: true);
        }

        private ApplicationUser GetUser()
        {
            string userName = UserName;
            if (string.IsNullOrEmpty(userName))
            {
                PrimaryKeyId contactId = ContactId;
                if (contactId == PrimaryKeyId.Empty)
                {
                    return null;
                }

                CustomerContact contact = CustomerContext.Current.GetContactById(contactId);
                userName = _mapUserKey.ToUserKey(contact.UserId) as string;
            }

            ApplicationUser user = _userManager.FindByName(userName);

            return user;
        }
    }
}