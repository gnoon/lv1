#define DEBUG

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Mail;
using System.IO;
using System.ComponentModel;
using System.Net;
using System.Reflection;
using System.Security.Principal;
using System.Globalization;
using Xipton.Razor;

namespace LeaveCore.Email.Factories
{
    public static class EmailFactory
    {
        public static string ParseTemplate(LeaveCore.Email.Model.IEmailModel model, string pathToTemplates, LeaveCore.Email.Services.EmailType emailType)
        {
            var templatePath = Path.Combine(pathToTemplates, string.Format("{0}.cshtml", emailType));
            var content = ReadTemplateContent(templatePath);
            var rm = new RazorMachine();
            return rm.ExecuteContent(content, model).Result;
        }

        public static string ReadTemplateContent(string path)
        {
            string content;
            using (var reader = new StreamReader(path))
            {
                content = reader.ReadToEnd();
            }
            return content;
        }
    }
}

namespace LeaveCore.Email.Model
{
    public interface IEmailModel
    {
        IIdentifiable GetUserToken();
    }

    public interface ILeaveEmailModel : IEmailModel
    {
        CultureInfo DisplayCultureInfo { get; }
        string InternetBasedUrl { get; set; }
    }

    public abstract class LeaveEmailModel : IdentifiableObject, ILeaveEmailModel
    {
        /// <summary>
        /// เนื้อเมลล์จะเป็นภาษาไทย ซึ่งจะแสดงชื่อเดือนเป็นภาษาไทย และแสดงปีพ.ศ.
        /// </summary>
        public CultureInfo DisplayCultureInfo { get { return new System.Globalization.CultureInfo("th-TH"); } }
        string _InternetBasedUrl;
        public string InternetBasedUrl {
            get {
                if (_InternetBasedUrl == null) return null;
                if (_InternetBasedUrl.EndsWith("/")) _InternetBasedUrl.Remove(_InternetBasedUrl.Length - 1);
                return _InternetBasedUrl;
            }
            set { _InternetBasedUrl = value; } }
        public abstract IIdentifiable GetUserToken();
    }

    public class VetoEmailModel : LeaveEmailModel
    {
        VetoRecord _Head;
        public VetoRecord Head { get { return _Head; } }

        RequestRecord _LeaveRequest;
        public RequestRecord LeaveRequest { get { return _LeaveRequest; } }

        public override IIdentifiable GetUserToken() { return this; }

        public VetoEmailModel(IPrincipal User, Int64 RequestID, VetoRecord Head)
        {
            _Head = Head;
            Initialize(User, RequestID);
        }

        private void Initialize(IPrincipal User, Int64 RequestID)
        {
            RequestRecord RequestRecord = Leave.GetHeader(User, RequestID);
            if (RequestRecord == null)
                throw new LeaveException(string.Format("Leave request not found ({0}).", RequestID), null);
            _LeaveRequest = RequestRecord;
        }
    }

    public class ChangeEmailModel : LeaveEmailModel
    {
        RequestRecord _LeaveRequest;
        public RequestRecord LeaveRequest { get { return _LeaveRequest; } }

        PersonRecord _Recipient;
        public PersonRecord Recipient { get { return _Recipient; } }

        string _ChangeMessage;
        public string ChangeMessage { get { return _ChangeMessage; } }

        public override IIdentifiable GetUserToken()
        {
            return this;
        }

        public ChangeEmailModel(IPrincipal User, Int64 RequestID, string RecipientPersonNo, string ChangeMessage)
        {
            _ChangeMessage = ChangeMessage;
            Initialize(User, RequestID, RecipientPersonNo);
        }

        private void Initialize(IPrincipal User, Int64 RequestID, string RecipientPersonNo)
        {
            _LeaveRequest = Leave.GetHeader(User, RequestID);
            if (_LeaveRequest == null)
                throw new LeaveException(string.Format("Leave request not found ({0}).", RequestID), null);

            _Recipient = Person.GetInfo(User, RecipientPersonNo, null);
            if (_Recipient == null)
                throw new LeaveException(string.Format("Employee's account not found ({0}).", RecipientPersonNo), null);
        }
    }

    public class NotificationEmailModel : LeaveEmailModel
    {
        List<RequestRecord> _PendingList;
        public List<RequestRecord> PendingList { get { return _PendingList; } }

        GrantorRecord _Head;
        public GrantorRecord Head { get { return _Head; } }

        public override IIdentifiable GetUserToken()
        {
            return this;
        }

        //public NotificationEmailModel(IPrincipal User, string HeadPersonNo)
        //{
        //    Initialize(User, HeadPersonNo);
        //}

        public NotificationEmailModel(IPrincipal User, string HeadPersonNo, Int64 RequestID)
        {
            Initialize(User, HeadPersonNo, RequestID);
        }

        private void Initialize(IPrincipal User, string HeadPersonNo, Int64 RequestID)
        {
            _Head = Grantors.GetGrantor(User, HeadPersonNo);
            if (_Head == null)
                throw new LeaveException(string.Format("Employee {0} not found.", HeadPersonNo), null);

            RequestRecord _LeaveRequest = Leave.GetHeader(User, RequestID);
            if (_LeaveRequest == null)
                throw new LeaveException(string.Format("Leave request not found ({0}).", RequestID), null);

            //_PendingList = Leave.ListApprove(User, HeadPersonNo, 1, 0);
            _PendingList = new List<RequestRecord>();
            _PendingList.Add(_LeaveRequest);
        }
    }


    public class FakeVetoEmailModel : VetoEmailModel
    {
        public FakeVetoEmailModel() : base(User, 1, VetoHead)
        {
        }
        static string EmpPersonNo = "nopadols";
        static string ConnStr {get{return "Data Source=(local);Initial Catalog=LEAVE;Integrated Security=True;";}}
        static IPrincipal User
        {
            get { return new GenericPrincipal(LoginIdentity.CreateIdentity(EmpPersonNo, "Web", ConnStr), null); }
        }
        static VetoRecord VetoHead
        {
            get { return Leave.GetVetoes(User, 1, null).DefaultIfEmpty(null).FirstOrDefault(); }
        }
    }

    public class FakeChangeEmailModel : ChangeEmailModel
    {
        public FakeChangeEmailModel()
            : base(User, 1, "anusorns", "ใบลาถูกอนุมัติเรียบร้อยแล้ว")
        {
        }
        static string EmpPersonNo = "nopadols";
        static string ConnStr { get { return "Data Source=(local);Initial Catalog=LEAVE;Integrated Security=True;"; } }
        static IPrincipal User
        {
            get { return new GenericPrincipal(LoginIdentity.CreateIdentity(EmpPersonNo, "Web", ConnStr), null); }
        }
    }

    public class FakeNotificationEmailModel : NotificationEmailModel
    {
        public FakeNotificationEmailModel()
            : base(User, "sayamdans", 1)
        {
        }
        static string EmpPersonNo = "nopadols";
        static string ConnStr { get { return "Data Source=(local);Initial Catalog=LEAVE;Integrated Security=True;"; } }
        static IPrincipal User
        {
            get { return new GenericPrincipal(LoginIdentity.CreateIdentity(EmpPersonNo, "Web", ConnStr), null); }
        }
    }
}

namespace LeaveCore.Email.Services
{
    public enum EmailType { Veto, Notification, Change }

    public interface IEmailService
    {
        void SendEmail(string to, string cc, string bcc, string subject, EmailType emailType, LeaveCore.Email.Model.ILeaveEmailModel model, IEmailSendResult callback);
    }

    public interface ISettings
    {
        string SubjectEncoding { get; set; }
        string BodyEncoding { get; set; }
        string DisplayNameEncoding { get; set; }
    }

    public interface ITemplatePath
    {
        string TemplatePath { get; }
    }

    public interface IEmailSendResult
    {
        void BeforeSend(object sender, EmailSendEventArgs e);
        void SendCompleted(object sender, AsyncCompletedEventArgs e);
    }

    public class ResourceTemplatePath : ITemplatePath
    {
        private string _path;

        public string TemplatePath
        {
            get
            {
                if (!string.IsNullOrEmpty(_path))
                    return _path;

                var path = GetAssemblyDirectory();
                _path = Path.GetFullPath(Path.Combine(path, @"..\Views", "EmailTemplates"));

                return _path;
            }
        }

        private string GetAssemblyDirectory()
        {
            var codeBase = Assembly.GetExecutingAssembly().CodeBase;
            var uri = new UriBuilder(codeBase);
            var dir = new DirectoryInfo(Uri.UnescapeDataString(uri.Path));
            return dir.Parent.FullName;
        }
    }

    public class DefaultSettings : ISettings
    {
        string _SubjectEncoding = "UTF-8";
        string _BodyEncoding = "UTF-8";
        string _DisplayNameEncoding = "UTF-8";
        public string SubjectEncoding { get { return _SubjectEncoding; } set { _SubjectEncoding = value; } }
        public string BodyEncoding { get { return _BodyEncoding; } set { _BodyEncoding = value; } }
        public string DisplayNameEncoding { get { return _DisplayNameEncoding; } set { _DisplayNameEncoding = value; } }
        public string InternetBasedUrl { get { return InternetBasedUrl; } set { InternetBasedUrl = value; } }
    }

    public class EmailSendEventArgs : EventArgs
    {
        string _To;
        public string To { get { return _To; } }
        string _ToPersonNo;
        public string ToPersonNo { get { return _ToPersonNo; } }
        string _Cc;
        public string Cc { get { return _Cc; } }
        string _Bcc;
        public string Bcc { get { return _Bcc; } }
        string _Subject;
        public string Subject { get { return _Subject; } }
        EmailType _EmailType;
        public EmailType EmailType { get { return _EmailType; } }
        string _SendID;
        public string SendID { get { return _SendID; } }
        MailMessage _Message;
        public MailMessage Message { get { return _Message; } }
        Int64? _RequestID;
        public Int64? RequestID { get { return _RequestID; } }

        public EmailSendEventArgs(string To, string ToPersonNo, string Cc, string Bcc, string Subject, EmailType EmailType, string SendID,
            MailMessage Message, Int64? RequestID)
            : base()
        {
            _To = To;
            _ToPersonNo = ToPersonNo;
            _Cc = Cc;
            _Bcc = Bcc;
            _Subject = Subject;
            _EmailType = EmailType;
            _SendID = SendID;
            _Message = Message;
            _RequestID = RequestID;
        }
    }

    public class EmailSendResult : IEmailSendResult
    {
        IPrincipal _User;
        public EmailSendResult(IPrincipal User)
        {
            _User = User;
        }

        #region IEmailSendResult Members

        public void BeforeSend(object sender, EmailSendEventArgs e)
        {
            try
            {
                EmailLog.New(_User, e.SendID, e.To, e.ToPersonNo, e.Cc, e.Bcc, e.Subject, e.EmailType.ToString(), e.Message, e.RequestID);
            }
            catch(Exception ex)
            {
                // IGNORE IN PROD
                // THROW IN DEBUG
#if DEBUG
                throw ex;
#endif
            }
        }

        public void SendCompleted(object sender, AsyncCompletedEventArgs e)
        {
            MessageToken token = (MessageToken)e.UserState;
            var x = token == null ? null : token.Token;
            if (x == null) return;

            string SendID = x.InstanceID;
            bool SendSeccess = !e.Cancelled && (e.Error == null);
            string Error = e.Cancelled ? "Cancelled by user" : (e.Error != null ? e.Error.Message : null);

            try
            {
                EmailLog.Update(_User, SendID, SendSeccess, Error);
            }
            catch (Exception ex)
            {
                // IGNORE IN PROD
                // THROW IN DEBUG
#if DEBUG
                throw ex;
#endif
            }
        }

        #endregion
    }

    public class MessageToken
    {
        public MailMessage Message { get; set; }
        public IIdentifiable Token { get; set; }
    }

    public class EmailService : IEmailService
    {
        IPrincipal _User;
        private readonly ITemplatePath _templatePath;
        private readonly ISettings _settings;

        public EmailService(IPrincipal User)
            : this(User, new ResourceTemplatePath(), new DefaultSettings())
        {
        }

        public EmailService(IPrincipal User, ITemplatePath templatePath, ISettings settings)
        {
            _templatePath = templatePath;
            _settings = settings;
            _User = User;
        }

        public void SendEmail(string to, string bcc, string subject, EmailType emailType, LeaveCore.Email.Model.ILeaveEmailModel model)
        {
            SendEmail(to, null, bcc, subject, emailType, model, new EmailSendResult(_User));
        }

        public void SendEmail(string to, string cc, string bcc, string subject, EmailType emailType, LeaveCore.Email.Model.ILeaveEmailModel model, IEmailSendResult callback)
        {
            var content = LeaveCore.Email.Factories.EmailFactory.ParseTemplate(model, _templatePath.TemplatePath, emailType);
            var message = BuildMessage(to, cc, bcc, subject, content);
            var client = new SmtpClient();
            var token = model == null ? null : model.GetUserToken();
            var sendid = token == null ? null : token.InstanceID;
            if (callback != null)
            {
                Int64? RequestID = null;
                string tops = null;
                RequestRecord head;
                List<RequestRecord> list;
                if (Tool.TryGetPropertyValue<RequestRecord>(model, "LeaveRequest", out head))
                    RequestID = head.RequestID;
                if (!RequestID.HasValue)
                    if (Tool.TryGetPropertyValue<List<RequestRecord>>(model, "PendingList", out list))
                        RequestID = (list != null && list.Count > 0) ? list[0].RequestID : (Int64?)null;

                if (model is LeaveCore.Email.Model.NotificationEmailModel)
                    tops = ((LeaveCore.Email.Model.NotificationEmailModel)model).Head.HeadPersonNo;
                else if (model is LeaveCore.Email.Model.VetoEmailModel)
                    tops = ((LeaveCore.Email.Model.VetoEmailModel)model).Head.Person.PersonNo;
                else if (model is LeaveCore.Email.Model.ChangeEmailModel)
                    tops = ((LeaveCore.Email.Model.ChangeEmailModel)model).Recipient.PersonNo;

                callback.BeforeSend(this, new EmailSendEventArgs(to, tops, cc, bcc, subject, emailType, sendid, message, RequestID));
                client.SendCompleted += new SendCompletedEventHandler(callback.SendCompleted);
            }
            client.SendAsync(message, new MessageToken { Message = message, Token = token });
        }

        private MailMessage BuildMessage(string to, string cc, string bcc, string subject, string content)
        {
            System.Text.Encoding DisplayNameEncoding = System.Text.Encoding.GetEncoding(_settings.DisplayNameEncoding);
            MailMessage m = new MailMessage() { IsBodyHtml = true, Subject = subject, Body = content};
            m.BodyEncoding = System.Text.Encoding.GetEncoding(_settings.BodyEncoding);
            m.SubjectEncoding = System.Text.Encoding.GetEncoding(_settings.SubjectEncoding);
            string[] addrs = to.Split(';');
            foreach (string addr in addrs)
                m.To.Add(new MailAddress(addr, addr, DisplayNameEncoding));
            m.ReplyToList.Add(new MailAddress("noreply@verasu.com", "Please No Reply"));
            if (!string.IsNullOrEmpty(cc))
            {
                addrs = cc.Split(';');
                foreach (string addr in addrs)
                    m.CC.Add(new MailAddress(addr, addr, DisplayNameEncoding));
            }
            if (!string.IsNullOrEmpty(bcc))
            {
                addrs = bcc.Split(';');
                foreach (string addr in addrs)
                    m.Bcc.Add(new MailAddress(addr, addr, DisplayNameEncoding));
            }
            return m;
        }
    }
}