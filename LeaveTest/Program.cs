using System;
using LeaveCore;
using System.Globalization;
using System.Collections.Generic;
using System.Security.Principal;
using System.Threading;
using LeaveCore.Email.Services;

namespace LeaveTest
{
    class Program
    {
        static void Main(string[] args)
        {
			//CultureInfo enCulture = new CultureInfo("en-US");
			//DateTime f = DateTime.ParseExact(
			//    "16/09/2013 09:00", "dd/MM/yyyy HH:mm", enCulture);
			//DateTime t = DateTime.ParseExact(
			//    "18/09/2013 18:00", "dd/MM/yyyy HH:mm", enCulture, DateTimeStyles.None);
			//var a = t.ToString("yyyyMMdd", enCulture);
			//var b = t.ToString("HH:mm:ss", enCulture);

			//List<DateTime> Dates = new List<DateTime>(Tool.GetDateRange(f, t));
			//Console.WriteLine(f.ToString(new CultureInfo("en-US")));
			//Console.WriteLine(t.ToString(new CultureInfo("en-US")));

			//string PersonNo = "anusorns";
			string ConnectionString = "Data Source=(local);Initial Catalog=LEAVE;User ID=sa;Password=devadmin";
			string roles = "MY|HR|HEAD|ASSTHEAD";
			//string roles = "MY|HR|HEAD|IMPERSONATE|ASSTHEAD";
			LoginIdentity Identity = LoginIdentity.CreateIdentity("anusorns", "Forms", ConnectionString);
			GenericPrincipal User = new GenericPrincipal(Identity, roles.Split(new char[] { '|' }));

			//LeaveCore.Leave obj = new LeaveCore.Leave(User, PersonNo);
			//obj.SetLeaveParams(
			//    PersonNo,
			//    2,
			//    new DateTime(2013, 9, 4, 9, 0, 0),
			//    new DateTime(2013, 9, 5, 18, 0, 0),
			//    "Test for leave",
			//    "Leave.Verasu",
			//    "");
			//Int64 Id = obj.NewLeaveRequest();

			//Console.WriteLine("Success ID : " + Id.ToString());


			//RequestRecord rec = LeaveCore.Leave.GetHeader(User, 4);


			//string str = "123@Grant";
			//string sOutput = Tool.CreateHash(str);
			//Console.WriteLine("MD5 Hash for \""+str+"\" is : " + sOutput + " ("+sOutput.Length+")");
			//Console.WriteLine("Compare \""+str+"\" to hash is : " + Tool.VerifyHash(str, sOutput).ToString());

			//Console.ReadLine();
			//Console.WriteLine(p.UntilDate.GetValueOrDefault(DateTime.MinValue).ToString(new CultureInfo("en-US")));


		}
	}
}
