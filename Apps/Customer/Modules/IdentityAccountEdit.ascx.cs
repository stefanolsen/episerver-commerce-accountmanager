using EPiServer.Cms.UI.AspNetIdentity;
using EPiServer.ServiceLocation;
using Mediachase.BusinessFoundation;
using Mediachase.BusinessFoundation.Data;
using Mediachase.Commerce.Customers;
using Mediachase.Commerce.Extensions;
using System;
using System.Reflection;
using System.Resources;
using System.Web.UI.WebControls;
using Microsoft.AspNet.Identity;

namespace StefanOlsen.Manager.Apps.Customer.Modules
{
    public partial class IdentityAccountEdit : MCDataBoundControl
    {
        private readonly MapUserKey _mapUserKey;
        private readonly ApplicationUserManager<ApplicationUser> _userManager;
        private readonly ResourceManager _resourceManagerShared;

        public IdentityAccountEdit()
            : this(
                ServiceLocator.Current.GetInstance<MapUserKey>(),
                ServiceLocator.Current.GetInstance<ApplicationUserManager<ApplicationUser>>())
        {
        }

        public IdentityAccountEdit(
            MapUserKey mapUserKey,
            ApplicationUserManager<ApplicationUser> userManager)
        {
            _mapUserKey = mapUserKey;
            _userManager = userManager;
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

        protected bool IsEditMode => CustomerContext.Current.GetUserForContactId(ContactId) != null;

        protected void Page_Load(object sender, EventArgs e)
        {
            lblErrorInfo.Text = string.Empty;
            if (!IsPostBack)
            {
                TrPassword.Visible = !IsEditMode;
                BindData();
            }

            BindButtons();
        }

        private void BindButtons()
        {
            btnSave.Text = GetGlobalResourceObject("Common", "btnOK").ToString();
            btnSave.CustomImage = Page.ResolveUrl("~/Apps/MetaDataBase/images/ok-button.gif");
            btnCancel.Text = GetGlobalResourceObject("Common", "btnCancel").ToString();
            btnCancel.CustomImage = Page.ResolveUrl("~/Apps/MetaDataBase/images/cancel-button.gif");
            btnCancel.Attributes.Add("onclick", CommandHandler.GetCloseOpenedFrameScript(Page, string.Empty, refreshParent: false, addReturn: true));
        }

        private void BindData()
        {
            ApplicationUser user = GetUser();
            if (user != null)
            {
                UserNameTextBox.Text = user.UserName;
                UserNameTextBox.Enabled = false;
                tbDescription.Text = user.Comment;
                tbEmailText.Text = user.Email;
                IsApproved.IsSelected = user.IsApproved;
            }
            else
            {
                CustomerContact contact = CustomerContext.Current.GetContactById(ContactId);
                if (contact != null)
                {
                    tbEmailText.Text = contact.GetHtmlEncodedEmail();
                }
            }
        }

        public void UserPassword_ServerValidate(object source, ServerValidateEventArgs args)
        {
            args.IsValid = false;
            string password = TbPassword.Text;

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

            IdentityResult result;
            ApplicationUser user = GetUser();
            if (user == null)
            {
                CustomerContact contact = CustomerContext.Current.GetContactById(ContactId);

                user = new ApplicationUser
                {
                    CreationDate = DateTime.UtcNow,
                    Username = tbEmailText.Text,
                    Email = tbEmailText.Text
                };

                result = _userManager.Create(user, TbPassword.Text);
                if (!result.Succeeded)
                {
                    lblErrorInfo.Text = string.Join(" ", result.Errors);
                }

                contact.UserId = new MapUserKey().ToTypedString(user.UserName);
                contact.SaveChanges();
            }

            user.Comment = tbDescription.Text;
            user.IsApproved = IsApproved.IsSelected;
            user.SecurityStamp = Guid.NewGuid().ToString();

            if (!string.Equals(user.Email, tbEmailText.Text, StringComparison.OrdinalIgnoreCase))
            {
                user.Email = tbEmailText.Text;
            }

            result = _userManager.Update(user);
            if (!result.Succeeded)
            {
                lblErrorInfo.Text = string.Join(" ", result.Errors);
            }

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
                if (contact == null)
                {
                    return null;
                }

                userName = _mapUserKey.ToUserKey(contact.UserId) as string;
                if (userName == null)
                {
                    return null;
                }
            }

            ApplicationUser user = _userManager.FindByName(userName);

            return user;
        }
    }
}