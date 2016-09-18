using System;
using System.Collections.Generic;
using System.Text;
using System.Data.SqlClient;
using System.Data;
using System.Globalization;
using System.Security.Principal;
using System.Net.Mail;
using LeaveCore.Email.Serialization;

namespace LeaveCore
{
    public class EmailLog : BaseDB
    {
        public EmailLog(IPrincipal User)
            : base(User)
        {
        }

        protected void New(string SendID, string To, string ToPersonNo, string Cc, string Bcc, string Subject, string EmailType, MailMessage Message, Int64? RequestID)
        {
            using (SqlConnection conn = GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.CommandText = "INSERT INTO [LV Mail Log]([ID],[Send Time],[To],[To PersonNo],[Cc],[Bcc],[Subject],[Type],[Send Result],[Error],[Request ID],[Message]) VALUES(@id,@time,@to,@tops,@cc,@bcc,@subject,@type,@result,@error,@reqid,@message)";
                    cmd.Parameters.Add("@id", SqlDbType.VarChar, 40).Value = SendID ?? (object)DBNull.Value;
                    cmd.Parameters.Add("@time", SqlDbType.DateTime).Value = DateTime.Now;
                    cmd.Parameters.Add("@to", SqlDbType.VarChar, 80).Value = To ?? string.Empty;
                    cmd.Parameters.Add("@tops", SqlDbType.VarChar, 80).Value = ToPersonNo ?? string.Empty;
                    cmd.Parameters.Add("@cc", SqlDbType.VarChar, 80).Value = Cc ?? string.Empty;
                    cmd.Parameters.Add("@bcc", SqlDbType.VarChar, 80).Value = Bcc ?? string.Empty;
                    cmd.Parameters.Add("@subject", SqlDbType.VarChar, 255).Value = Subject ?? string.Empty;
                    cmd.Parameters.Add("@type", SqlDbType.VarChar, 40).Value = EmailType ?? string.Empty;
                    cmd.Parameters.Add("@result", SqlDbType.VarChar, 7).Value = "Sending";
                    cmd.Parameters.Add("@error", SqlDbType.VarChar, 255).Value = string.Empty;
                    cmd.Parameters.Add("@reqid", SqlDbType.BigInt).Value = RequestID.HasValue ? RequestID : (object)DBNull.Value;
                    try
                    {
                        cmd.Parameters.Add("@message", SqlDbType.Text).Value = Tool.SerializeToXml(new SerializableMailMessage(Message));
                    }
                    catch
                    {
                        cmd.Parameters.Add("@message", SqlDbType.Text).Value = Message.Body;
                    }
                    cmd.ExecuteNonQuery();
                }
            }
        }

        protected void Update(string SendID, bool SendSeccess, string Error)
        {
            using (SqlConnection conn = GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandType = CommandType.Text;
                    // ถ้าส่งเมลล์ได้สำเร็จจะลบ Message ออกจากฐานข้อมูลเพื่อประหยัดเนื้อที่
                    cmd.CommandText = "UPDATE [LV Mail Log] SET [Send Result]=@result,[Error]=@error,"+
                        "[Message]=CASE WHEN @result='Success' THEN NULL ELSE [Message] END WHERE [ID]=@id";
                    cmd.Parameters.Add("@id", SqlDbType.VarChar, 40).Value = SendID ?? (object)DBNull.Value;
                    cmd.Parameters.Add("@result", SqlDbType.VarChar, 7).Value = SendSeccess ? "Success" : "Failed";
                    cmd.Parameters.Add("@error", SqlDbType.VarChar, 255).Value = Error ?? string.Empty;
                    cmd.ExecuteNonQuery();
                }
            }
        }

        protected List<EmailLogRecord> GetLog(Int64? RequestID)
        {
            List<EmailLogRecord> list = new List<EmailLogRecord>();
            using (SqlConnection conn = GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.CommandText =
                        "SELECT a.[ID],a.[Send Time],a.[To],a.[To PersonNo],a.[Cc],a.[Bcc],a.[Subject],a.[Type],a.[Send Result]," +
                        "a.[Error],a.[Request ID],a.[Message],b.[TH Prefix],b.[TH First Name],b.[TH Last Name] " +
                        "FROM [LV Mail Log] a WITH(READUNCOMMITTED) " +
                        "LEFT OUTER JOIN [HR Person] b WITH(READUNCOMMITTED) ON a.[To PersonNo]=b.[Person No] " +
                        "WHERE a.[Request ID]=@reqid ORDER BY [Type]";
                    cmd.Parameters.Add("@reqid", SqlDbType.BigInt).Value = RequestID.HasValue ? RequestID : (object)DBNull.Value;
                    using (SqlDataReader rs = cmd.ExecuteReader())
                    {
                        EmailLogRecord rec;
                        string name, mail;
                        //MailAddress mailAcc;
                        while (rs.Read())
                        {
                            rec = new EmailLogRecord();
                            rec.ID = rs.GetValue<string>("ID");
                            rec.SendTime = rs.GetValue<DateTime>("Send Time");
                            rec.To = rs.GetValue<string>("To");
                            rec.ToPersonNo = rs.GetValue<string>("To PersonNo");
                            rec.ToName = rs.GetValue<string>("TH First Name");
                            name = "หัวหน้างาน";
                            mail = rec.To;
                            try
                            {
                                //mailAcc = new MailAddress(rec.To);
                                //mail = mailAcc.Address;
                                if (!string.IsNullOrWhiteSpace(rec.ToName))
                                    name = string.Format("{0} {1}  {2}", "คุณ", rec.ToName, rs.GetValue<string>("TH Last Name"));
                                //else if (!string.IsNullOrWhiteSpace(mailAcc.DisplayName))
                                //    name = mailAcc.DisplayName;
                            }
                            catch { }
                            rec.ToName = string.Format("{0} ({1})", name, mail);
                            rec.Cc = rs.GetValue<string>("Cc");
                            rec.Bcc = rs.GetValue<string>("Bcc");
                            rec.Subject = rs.GetValue<string>("Subject");
                            rec.Type = rs.GetValue<string>("Type");
                            rec.SendResult = rs.GetValue<string>("Send Result");
                            rec.Error = rs.GetValue<string>("Error");
                            rec.RequestID = rs.GetValue<Int64?>("Request ID");
                            rec.Message = rs.GetValue<string>("Message");
                            list.Add(rec);
                        }
                    }
                }
            }
            return list;
        }

        public static void New(IPrincipal User, string SendID, string To, string ToPersonNo, string Cc, string Bcc, string Subject, string EmailType, MailMessage message, Int64? RequestID)
        {
            new EmailLog(User).New(SendID, To, ToPersonNo, Cc, Bcc, Subject, EmailType, message, RequestID);
        }

        public static void Update(IPrincipal User, string SendID, bool SendSeccess, string Error)
        {
            new EmailLog(User).Update(SendID, SendSeccess, Error);
        }

        public static List<EmailLogRecord> GetLog(IPrincipal User, Int64? RequestID)
        {
            return new EmailLog(User).GetLog(RequestID);
        }
    }
}
