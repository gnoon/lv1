﻿/**
 * Original work: http://meandaspnet.blogspot.com/2009/10/how-to-binary-serialize-mailmessage-for.html
 * Adatped for Framework 4.0
 */
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using System.Runtime.Serialization;

namespace LeaveCore.Email.Serialization
{
    [Serializable]
    internal class SerializeableLinkedResource
    {
        String ContentId;
        Uri ContentLink;
        Stream ContentStream;
        SerializeableContentType ContentType;
        TransferEncoding TransferEncoding;

        internal static SerializeableLinkedResource GetSerializeableLinkedResource(LinkedResource lr)
        {
            if (lr == null)
                return null;

            SerializeableLinkedResource slr = new SerializeableLinkedResource();
            slr.ContentId = lr.ContentId;
            slr.ContentLink = lr.ContentLink;

            if (lr.ContentStream != null)
            {
                byte[] bytes = new byte[lr.ContentStream.Length];
                lr.ContentStream.Read(bytes, 0, bytes.Length);
                slr.ContentStream = new MemoryStream(bytes);
            }

            slr.ContentType = SerializeableContentType.GetSerializeableContentType(lr.ContentType);
            slr.TransferEncoding = lr.TransferEncoding;

            return slr;

        }

        internal LinkedResource GetLinkedResource()
        {
            LinkedResource slr = new LinkedResource(ContentStream);
            slr.ContentId = ContentId;
            slr.ContentLink = ContentLink;

            slr.ContentType = ContentType.GetContentType();
            slr.TransferEncoding = TransferEncoding;

            return slr;
        }
    }

    [Serializable]
    internal class SerializeableAlternateView
    {

        Uri BaseUri;
        String ContentId;
        Stream ContentStream;
        SerializeableContentType ContentType;
        List<SerializeableLinkedResource> LinkedResources = new List<SerializeableLinkedResource>();
        TransferEncoding TransferEncoding;

        internal static SerializeableAlternateView GetSerializeableAlternateView(AlternateView av)
        {
            if (av == null)
                return null;

            SerializeableAlternateView sav = new SerializeableAlternateView();

            sav.BaseUri = av.BaseUri;
            sav.ContentId = av.ContentId;

            if (av.ContentStream != null)
            {
                byte[] bytes = new byte[av.ContentStream.Length];
                av.ContentStream.Read(bytes, 0, bytes.Length);
                sav.ContentStream = new MemoryStream(bytes);
            }

            sav.ContentType = SerializeableContentType.GetSerializeableContentType(av.ContentType);

            foreach (LinkedResource lr in av.LinkedResources)
                sav.LinkedResources.Add(SerializeableLinkedResource.GetSerializeableLinkedResource(lr));

            sav.TransferEncoding = av.TransferEncoding;
            return sav;
        }

        internal AlternateView GetAlternateView()
        {

            AlternateView sav = new AlternateView(ContentStream);

            sav.BaseUri = BaseUri;
            sav.ContentId = ContentId;

            sav.ContentType = ContentType.GetContentType();

            foreach (SerializeableLinkedResource lr in LinkedResources)
                sav.LinkedResources.Add(lr.GetLinkedResource());

            sav.TransferEncoding = TransferEncoding;
            return sav;
        }
    }

    [Serializable]
    internal class SerializeableMailAddress
    {
        String User;
        String Host;
        String Address;
        String DisplayName;

        internal static SerializeableMailAddress GetSerializeableMailAddress(MailAddress ma)
        {
            if (ma == null)
                return null;
            SerializeableMailAddress sma = new SerializeableMailAddress();

            sma.User = ma.User;
            sma.Host = ma.Host;
            sma.Address = ma.Address;
            sma.DisplayName = ma.DisplayName;
            return sma;
        }

        internal MailAddress GetMailAddress()
        {
            return new MailAddress(Address, DisplayName);
        }
    }

    [Serializable]
    internal class SerializeableContentDisposition
    {
        DateTime CreationDate;
        String DispositionType;
        String FileName;
        Boolean Inline;
        DateTime ModificationDate;
        SerializeableCollection Parameters;
        DateTime ReadDate;
        long Size;

        internal static SerializeableContentDisposition GetSerializeableContentDisposition(System.Net.Mime.ContentDisposition cd)
        {
            if (cd == null)
                return null;

            SerializeableContentDisposition scd = new SerializeableContentDisposition();
            scd.CreationDate = cd.CreationDate;
            scd.DispositionType = cd.DispositionType;
            scd.FileName = cd.FileName;
            scd.Inline = cd.Inline;
            scd.ModificationDate = cd.ModificationDate;
            scd.Parameters = SerializeableCollection.GetSerializeableCollection(cd.Parameters);
            scd.ReadDate = cd.ReadDate;
            scd.Size = cd.Size;

            return scd;
        }

        internal void SetContentDisposition(ContentDisposition scd)
        {
            scd.CreationDate = CreationDate;
            scd.DispositionType = DispositionType;
            scd.FileName = FileName;
            scd.Inline = Inline;
            scd.ModificationDate = ModificationDate;
            Parameters.SetColletion(scd.Parameters);

            scd.ReadDate = ReadDate;
            scd.Size = Size;
        }
    }

    [Serializable]
    internal class SerializeableContentType
    {
        String Boundary;
        String CharSet;
        String MediaType;
        String Name;
        SerializeableCollection Parameters;

        internal static SerializeableContentType GetSerializeableContentType(System.Net.Mime.ContentType ct)
        {
            if (ct == null)
                return null;

            SerializeableContentType sct = new SerializeableContentType();

            sct.Boundary = ct.Boundary;
            sct.CharSet = ct.CharSet;
            sct.MediaType = ct.MediaType;
            sct.Name = ct.Name;
            sct.Parameters = SerializeableCollection.GetSerializeableCollection(ct.Parameters);

            return sct;
        }

        internal ContentType GetContentType()
        {

            ContentType sct = new ContentType();

            sct.Boundary = Boundary;
            sct.CharSet = CharSet;
            sct.MediaType = MediaType;
            sct.Name = Name;

            Parameters.SetColletion(sct.Parameters);

            return sct;
        }
    }

    [Serializable]
    internal class SerializeableAttachment
    {
        String ContentId;
        SerializeableContentDisposition ContentDisposition;
        SerializeableContentType ContentType;
        Stream ContentStream;
        System.Net.Mime.TransferEncoding TransferEncoding;
        String Name;
        Encoding NameEncoding;

        internal static SerializeableAttachment GetSerializeableAttachment(Attachment att)
        {
            if (att == null)
                return null;

            SerializeableAttachment saa = new SerializeableAttachment();
            saa.ContentId = att.ContentId;
            saa.ContentDisposition = SerializeableContentDisposition.GetSerializeableContentDisposition(att.ContentDisposition);

            if (att.ContentStream != null)
            {
                byte[] bytes = new byte[att.ContentStream.Length];
                att.ContentStream.Read(bytes, 0, bytes.Length);

                saa.ContentStream = new MemoryStream(bytes);
            }

            saa.ContentType = SerializeableContentType.GetSerializeableContentType(att.ContentType);
            saa.Name = att.Name;
            saa.TransferEncoding = att.TransferEncoding;
            saa.NameEncoding = att.NameEncoding;
            return saa;
        }

        internal Attachment GetAttachment()
        {
            Attachment saa = new Attachment(ContentStream, Name);
            saa.ContentId = ContentId;
            this.ContentDisposition.SetContentDisposition(saa.ContentDisposition);

            saa.ContentType = ContentType.GetContentType();
            saa.Name = Name;
            saa.TransferEncoding = TransferEncoding;
            saa.NameEncoding = NameEncoding;
            return saa;
        }
    }

    [Serializable]
    internal class SerializeableCollection
    {
        Dictionary<string, string> Collection = new Dictionary<string, string>();
        internal SerializeableCollection()
        {

        }

        internal static SerializeableCollection GetSerializeableCollection(NameValueCollection col)
        {
            
            if (col == null)
                return null;

            SerializeableCollection scol = new SerializeableCollection();
            foreach (String key in col.Keys)
                scol.Collection.Add(key, col[key]);

            return scol;
        }

        internal static SerializeableCollection GetSerializeableCollection(StringDictionary col)
        {
            if (col == null)
                return null;

            SerializeableCollection scol = new SerializeableCollection();
            foreach (String key in col.Keys)
                scol.Collection.Add(key, col[key]);

            return scol;
        }

        internal void SetColletion(NameValueCollection scol)
        {

            foreach (String key in Collection.Keys)
            {
                scol.Add(key, this.Collection[key]);
            }

        }

        internal void SetColletion(StringDictionary scol)
        {

            foreach (String key in Collection.Keys)
            {
                if (scol.ContainsKey(key))
                    scol[key] = Collection[key];
                else
                    scol.Add(key, this.Collection[key]);
            }
        }
    }

    ///

    /// Serializeable mailmessage object
    ///

    [DataContract]
    public class SerializableMailMessage
    {
        [DataMember]
        Boolean IsBodyHtml { get; set; }
        [DataMember]
        String Body { get; set; }
        [DataMember]
        SerializeableMailAddress From { get; set; }
        [DataMember]
        List<SerializeableMailAddress> To = new List<SerializeableMailAddress>();
        [DataMember]
        List<SerializeableMailAddress> CC = new List<SerializeableMailAddress>();
        [DataMember]
        List<SerializeableMailAddress> Bcc = new List<SerializeableMailAddress>();
        [DataMember]
        List<SerializeableMailAddress> ReplyToList = new List<SerializeableMailAddress>();
        [DataMember]
        SerializeableMailAddress Sender { get; set; }
        [DataMember]
        String Subject { get; set; }
        [DataMember]
        List<SerializeableAttachment> Attachments = new List<SerializeableAttachment>();
        [DataMember]
        String BodyEncoding;
        [DataMember]
        String SubjectEncoding;
        [DataMember]
        DeliveryNotificationOptions DeliveryNotificationOptions;
        [DataMember]
        SerializeableCollection Headers;
        [DataMember]
        MailPriority Priority;
        [DataMember]
        List<SerializeableAlternateView> AlternateViews = new List<SerializeableAlternateView>();

        /// <summary>
        /// Just to use with XmlSerializer
        /// </summary>
        private SerializableMailMessage()
        {
        }

        ///
        /// Creates a new serializeable mailmessage based on a MailMessage object
        ///
        /// 
        public SerializableMailMessage(MailMessage mm)
        {
            IsBodyHtml = mm.IsBodyHtml;
            Body = mm.Body;
            Subject = mm.Subject;
            From = SerializeableMailAddress.GetSerializeableMailAddress(mm.From);
            To = new List<SerializeableMailAddress>();
            foreach (MailAddress ma in mm.To)
            {
                To.Add(SerializeableMailAddress.GetSerializeableMailAddress(ma));
            }

            CC = new List<SerializeableMailAddress>();
            foreach (MailAddress ma in mm.CC)
            {
                CC.Add(SerializeableMailAddress.GetSerializeableMailAddress(ma));
            }

            Bcc = new List<SerializeableMailAddress>();
            foreach (MailAddress ma in mm.Bcc)
            {
                Bcc.Add(SerializeableMailAddress.GetSerializeableMailAddress(ma));
            }

            Attachments = new List<SerializeableAttachment>();
            foreach (Attachment att in mm.Attachments)
            {
                Attachments.Add(SerializeableAttachment.GetSerializeableAttachment(att));
            }

            ReplyToList = new List<SerializeableMailAddress>();
            foreach (MailAddress ma in mm.ReplyToList)
            {
                ReplyToList.Add(SerializeableMailAddress.GetSerializeableMailAddress(ma));
            }

            BodyEncoding = mm.BodyEncoding.WebName;

            DeliveryNotificationOptions = mm.DeliveryNotificationOptions;
            Headers = SerializeableCollection.GetSerializeableCollection(mm.Headers);
            Priority = mm.Priority;
            Sender = SerializeableMailAddress.GetSerializeableMailAddress(mm.Sender);
            SubjectEncoding = mm.SubjectEncoding.WebName;

            foreach (AlternateView av in mm.AlternateViews)
                AlternateViews.Add(SerializeableAlternateView.GetSerializeableAlternateView(av));
        }

        public MailMessage GetMailMessage()
        {
            MailMessage mm = new MailMessage();

            mm.IsBodyHtml = IsBodyHtml;
            mm.Body = Body;
            mm.Subject = Subject;
            if (From != null)
                mm.From = From.GetMailAddress();

            foreach (SerializeableMailAddress ma in To)
            {
                mm.To.Add(ma.GetMailAddress());
            }

            foreach (SerializeableMailAddress ma in CC)
            {
                mm.CC.Add(ma.GetMailAddress());
            }

            foreach (SerializeableMailAddress ma in Bcc)
            {
                mm.Bcc.Add(ma.GetMailAddress());
            }

            foreach (SerializeableMailAddress ma in ReplyToList)
            {
                mm.ReplyToList.Add(ma.GetMailAddress());
            }

            foreach (SerializeableAttachment att in Attachments)
            {
                mm.Attachments.Add(att.GetAttachment());
            }

            mm.BodyEncoding = Encoding.GetEncoding(BodyEncoding);

            mm.DeliveryNotificationOptions = DeliveryNotificationOptions;
            Headers.SetColletion(mm.Headers);
            mm.Priority = Priority;

            if (Sender != null)
                mm.Sender = Sender.GetMailAddress();

            mm.SubjectEncoding = Encoding.GetEncoding(SubjectEncoding);

            foreach (SerializeableAlternateView av in AlternateViews)
                mm.AlternateViews.Add(av.GetAlternateView());

            return mm;
        }

    }
}