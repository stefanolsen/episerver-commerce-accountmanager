using EPiServer.Cms.UI.AspNetIdentity;
using EPiServer.Security;
using EPiServer.ServiceLocation;
using Mediachase.BusinessFoundation;
using Mediachase.BusinessFoundation.Data;
using Mediachase.Commerce;
using Mediachase.Commerce.Customers;
using Mediachase.Commerce.Security;
using Mediachase.Ibn.Web.UI;
using Mediachase.Web.Console.Common;
using Microsoft.AspNet.Identity;
using System;
using System.Collections.Generic;
using System.Text;
using System.Web.UI;
using System.Web.UI.HtmlControls;

namespace StefanOlsen.Manager.Apps.Customer.Modules
{
    public partial class IdentityAccountView : UserControl
    {
        private readonly MapUserKey _mapUserKey;
        private readonly ApplicationUserManager<ApplicationUser> _userManager;

        public IdentityAccountView()
            : this(
                ServiceLocator.Current.GetInstance<MapUserKey>(),
                ServiceLocator.Current.GetInstance<ApplicationUserManager<ApplicationUser>>())
        {
        }

        public IdentityAccountView(
            MapUserKey mapUserKey,
            ApplicationUserManager<ApplicationUser> userManager)
        {
            _mapUserKey = mapUserKey;
            _userManager = userManager;
        }

        public PrimaryKeyId EntityId =>
            Request.QueryString["ObjectId"] != null
                ? PrimaryKeyId.Parse(Request.QueryString["ObjectId"])
                : PrimaryKeyId.Empty;

        protected void Page_PreRender(object sender, EventArgs e)
        {
            BindData();
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("function fn_rebind(params){");
            stringBuilder.Append(Page.ClientScript.GetPostBackEventReference(btnRefresh, ""));
            stringBuilder.Append("}");

            ScriptManager.RegisterStartupScript(
                Page,
                Page.GetType(),
                Guid.NewGuid().ToString("N"),
                stringBuilder.ToString(),
                addScriptTags: true);

            btnDeleteAccount.Click += btnDeleteAccount_Click;
        }

        private string GetCmdManagerCommand(string cmdName, Dictionary<string, string> parameters)
        {
            return CommandManager.GetCurrent(Page)
                .AddCommand("Contact", "EntityView", "ContactIdentityAccount-List", new CommandParameters(cmdName, parameters));
        }

        private void BindData()
        {
            Dictionary<string, string> dictionary = new Dictionary<string, string>(2)
            {
                {"ContactId", EntityId.ToString()}
            };

            ApplicationUser user = GetUser(EntityId);
            if (user != null)
            {
                dictionary.Add("UserName", user.UserName);
            }

            string cmdManagerCommand = GetCmdManagerCommand("IdentityAccount_New", dictionary);
            string cmdManagerCommand2 = GetCmdManagerCommand("IdentityAccount_Edit", dictionary);
            string cmdManagerCommand3 = GetCmdManagerCommand("IdentityAccount_ChangePassword", dictionary);
            string cmdManagerCommand4 = GetCmdManagerCommand("IdentityAccount_Unlock", dictionary);

            if (user == null)
            {
                CustomerContact contact = CustomerContext.Current.GetContactById(EntityId);
                if (!string.IsNullOrEmpty(contact?.UserId))
                {
                    if (_mapUserKey.ToUserKey(contact.UserId) is string userId)
                    {
                        HtmlTableRow htmlTableRow = trNoAccount;
                        HtmlTableRow htmlTableRow2 = trDescription;
                        HtmlTableRow htmlTableRow3 = trEmail;
                        btnDeleteAccount.Visible = false;
                        htmlTableRow3.Visible = false;
                        htmlTableRow2.Visible = false;
                        htmlTableRow.Visible = false;
                        trUsername.Visible = true;
                        lblUserName.Text = userId;
                        IsLockedOut.Style.Add(HtmlTextWriterStyle.Color, "inherit");
                        IsLockedOut.Text = GetGlobalResourceObject("Customer", "MembershipUser_IsLockedOutFalse").ToString();
                        return;
                    }
                }
                trNoAccount.Visible = true;
                trUsername.Visible = false;
                trDescription.Visible = false;
                trEmail.Visible = false;
                btnDeleteAccount.Visible = false;

                if (PrincipalInfo.Current.IsPermitted(permission => permission.Customers.ContactsEdit))
                {
                    bhl.AddRightLink(GetGlobalResourceObject("Customer", "NewAccount").ToString(), "javascript:" + cmdManagerCommand);
                }

                return;
            }

            trNoAccount.Visible = false;
            trUsername.Visible = true;
            trDescription.Visible = true;
            trEmail.Visible = true;
            lblDecr.Text = user.Comment;
            lblEmail.Text = user.Email;
            lblUserName.Text = user.UserName;

            if (user.LastLoginDate.HasValue)
            {
                LastLoginDate.Text = ManagementHelper.FormatDateTime(user.LastLoginDate.Value);
            }

            if (user.LockoutEnabled && user.IsLockedOut && user.LastLockoutDate.HasValue)
            {
                string arg = ManagementHelper.FormatDateTime(user.LastLockoutDate.Value);
                IsLockedOut.Style.Add(HtmlTextWriterStyle.Color, "red");
                IsLockedOut.Text = string.Format(GetGlobalResourceObject("Customer", "MembershipUser_IsLockedOutTrue").ToString(), arg);

                if (PrincipalInfo.Current.IsPermitted((Permissions permission) => permission.Customers.ContactsEdit))
                {
                    bhl.AddRightLink(GetGlobalResourceObject("Customer", "UnlockUser").ToString(), "javascript:" + cmdManagerCommand4);
                }
            }
            else
            {
                IsLockedOut.Style.Add(HtmlTextWriterStyle.Color, "inherit");
                IsLockedOut.Text = GetGlobalResourceObject("Customer", "MembershipUser_IsLockedOutFalse").ToString();
            }

            if (PrincipalInfo.Current.IsPermitted((Permissions permission) => permission.Customers.ContactsEdit))
            {
                bhl.AddRightLink(GetGlobalResourceObject("Customer", "EditAccount").ToString(), "javascript:" + cmdManagerCommand2);
                bhl.AddRightLink(GetGlobalResourceObject("Customer", "ChangePassword").ToString(), "javascript:" + cmdManagerCommand3);
            }

            if (PrincipalInfo.Current.IsPermitted((Permissions permission) => permission.Customers.ContactsDelete))
            {
                bhl.AddRightLink(GetGlobalResourceObject("Customer", "DeleteAccount").ToString(), "javascript:" + Page.ClientScript.GetPostBackEventReference(btnDeleteAccount, string.Empty));
            }
        }

        protected void btnRefresh_OnClick(object sender, EventArgs e)
        {
            CHelper.RequireDataBind();
        }

        private void btnDeleteAccount_Click(object sender, EventArgs e)
        {
            ApplicationUser user = GetUser(EntityId);
            if (user != null)
            {
                // Do not allow to delete the master admin user or contact from here.
                if (string.Equals(user.Username, "admin", StringComparison.InvariantCultureIgnoreCase))
                {
                    return;
                }

                _userManager.Delete(user);
                CHelper.RequireDataBind();
            }

            CustomerContact contactById = CustomerContext.Current.GetContactById(EntityId);
            if (contactById != null)
            {
                contactById.UserId = Guid.NewGuid().ToString();
                contactById.SaveChanges();
            }
        }

        private ApplicationUser GetUser(Guid contactId)
        {
            CustomerContact contact = CustomerContext.Current.GetContactById(contactId);
            if (contact == null)
            {
                return null;
            }

            string userName = _mapUserKey.ToUserKey(contact.UserId) as string;
            if (userName == null)
            {
                return null;
            }

            ApplicationUser user = _userManager.FindByName(userName);

            return user;
        }
    }
}