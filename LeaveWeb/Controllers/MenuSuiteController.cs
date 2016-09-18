using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using LeaveCore;

namespace Leave.Controllers
{
	public class MenuSuiteController : Controller
    {
        //
        // GET: /MenuSuite/

		public MenuSuiteController()
		{
		}

        public ActionResult Index()
        {
			List<MenuSuiteRecord> Records = new List<MenuSuiteRecord>();
			if (User.Identity.IsAuthenticated)
			{
				MenuSuiteRecord rec;
				ItemRecord recItems;
				ItemSubRecord recItemsSub;
				LoginIdentity id = (LoginIdentity)User.Identity;
				foreach (var obj in GetRecords())
				{
					if (Convert.ToBoolean(obj.Active))
					{
						rec = new MenuSuiteRecord();
						rec.MenuID = obj.MenuID;
						rec.NameTH = obj.NameTH;

						rec.Items = new List<ItemRecord>();
						var filtersItems = obj.Items.Where(r => r.Roles.Any(n => id.Roles.Contains(n.NameTH))).ToList();
						if (filtersItems != null)
						{
							foreach (var item in filtersItems)
							{
								if (Convert.ToBoolean(item.Active))
								{
									recItems = new ItemRecord();
									recItems.NameTH = item.NameTH;
									recItems.ItemsSub = new List<ItemSubRecord>();
									if (item.ItemsSub != null)
									{
										foreach (var itemsub in item.ItemsSub)
										{
											if (Convert.ToBoolean(itemsub.Active))
											{
												recItemsSub = new ItemSubRecord();
												recItemsSub.NameTH = itemsub.NameTH;
												recItemsSub.Url = itemsub.Url;
												recItemsSub.Target = itemsub.Target;
												recItems.ItemsSub.Add(recItemsSub);
											}
										}
									}
									rec.Items.Add(recItems);
								}
							}
						}
						Records.Add(rec);
					}
				}
			}
			return View(Records);
        }

		private List<MenuSuiteRecord> GetRecords()
		{
			List<MenuSuiteRecord> Records = new List<MenuSuiteRecord>()
			{
				new MenuSuiteRecord() // Leave
				{
					MenuID = 1,
					NameTH = "ระบบลาออนไลน์",
					Sorting = 1,
					Active = 1,
					#region Menu Items
					Items = new List<ItemRecord>()
					{
						#region 1. พนักงาน (MY)
						new ItemRecord()
						{
							NameTH = "พนักงาน",
							Sorting = 1,
							Active = 1,
							Roles = new List<RoleRecord>()
							{
								new RoleRecord { NameTH = Const.ROLE_MY }
							},
							ItemsSub = new List<ItemSubRecord>()
							{
								new ItemSubRecord()
								{
									NameTH = "ยื่นใบลา",
									Sorting = 1,
									Active = 1,
									Url = Url.Content("~/Leave/Index")
								},
								new ItemSubRecord()
								{
									NameTH = "ติดตามใบลา",
									Sorting = 2,
									Active = 1,
									Url = Url.Content("~/Leave/Recents")
								},
								new ItemSubRecord()
								{
									NameTH = "ค้นหาประวัติการลา",
									Sorting = 3,
									Active = 1,
									Url = Url.Content("~/Leave/Search")
								}/*,
				                new ItemSubRecord()
				                {
				                    NameTH = "สถิติขาด ลา มาสาย",
									Sorting = 1,
									Active = Leave.Properties.Settings.Default.EnabledTIMEATTMenuSuite,
				                    Url = Url.Content("~/Attendance/Index")
				                },
				                new ItemSubRecord()
				                {
				                    NameTH = "สรุปเวลาทำงานรายวัน",
									Sorting = 1,
									Active = Leave.Properties.Settings.Default.EnabledTIMEATTMenuSuite,
				                    Url = Url.Content("~/Attendance/Daily")
				                }*/
							}
						},
						#endregion
						#region 2. หัวหน้างาน (HEAD)
						new ItemRecord()
						{
							NameTH = "หัวหน้างาน",
							Sorting = 2,
							Active = 1,
							Roles = new List<RoleRecord>()
							{
								new RoleRecord { NameTH = Const.ROLE_HEAD }
							},
							ItemsSub = new List<ItemSubRecord>()
							{
								new ItemSubRecord()
								{
									NameTH = "อนุมัติใบลา",
									Sorting = 1,
									Active = 1,
									Url = Url.Content("~/Grant/Index")
								},
								new ItemSubRecord()
								{
									NameTH = "ดูใบลาพนักงาน",
									Sorting = 1,
									Active = 1,
									Url = Url.Content("~/Leave/Underling")
								},
								new ItemSubRecord()
								{
									NameTH = "ตั้งค่าข้อมูลพนักงาน",
									Sorting = 1,
									Active = 1,
									Url = Url.Content("~/Setting/Underling")
								},
								new ItemSubRecord()
								{
									NameTH = "ตารางเวลาวันลาของพนักงาน",
									Sorting = 1,
									Active = 1,
									Url = Url.Content("~/Leave/Calendar")
								}
							}
						},
						#endregion
						#region 3. จัดการข้อมูล (HR)
						new ItemRecord()
						{
							NameTH = "จัดการข้อมูล",
							Sorting = 3,
							Active = 1,
							Roles = new List<RoleRecord>()
							{
								new RoleRecord { NameTH = Const.ROLE_HR },
								new RoleRecord { NameTH = Const.ROLE_IMPERSONATE }
							},
							ItemsSub = new List<ItemSubRecord>()
							{
								new ItemSubRecord()
								{
									NameTH = "แก้ไขใบลา",
									Sorting = 1,
									Active = 1,
									Url = Url.Content("~/Leave/Rechecked")
								},
								new ItemSubRecord()
								{
									NameTH = "ตั้งค่าข้อมูลพนักงาน",
									Sorting = 2,
									Active = 1,
									Url = Url.Content("~/Setting/Profiles")
								},
								new ItemSubRecord()
								{
									NameTH = "ตั้งค่าข้อมูลการลา",
									Sorting = 3,
									Active = 1,
									Url = Url.Content("~/Setting/Templates")
								}
							}
						},
						#endregion
						#region 3. จัดการข้อมูล (ASST HR)
						new ItemRecord()
						{
							NameTH = "จัดการข้อมูล", Sorting = 3,
							Roles = new List<RoleRecord>()
							{
								new RoleRecord { NameTH = Const.ROLE_ASSTHR },
							},
							ItemsSub = new List<ItemSubRecord>()
							{
								new ItemSubRecord()
								{
									NameTH = "ตั้งค่าข้อมูลพนักงาน",
									Sorting = 2,
									Active = 1,
									Url = Url.Content("~/Setting/Profiles")
								}
							}
						},
						#endregion
						#region 4. ออกรายงาน (HR)
						new ItemRecord()
						{
							NameTH = "ออกรายงาน",
							Sorting = 4,
							Active = 1,
							Roles = new List<RoleRecord>()
							{
								new RoleRecord { NameTH = Const.ROLE_HR },
								new RoleRecord { NameTH = Const.ROLE_IMPERSONATE }
							},
							ItemsSub = new List<ItemSubRecord>()
							{
								new ItemSubRecord()
								{
									NameTH = "Export ใบลาพนักงาน",
									Sorting = 1,
									Active = 1,
									Url = Leave.Properties.Settings.Default.LinkReportExport
								},/*
                                new ItemSubRecord()
								{
									NameTH = "สรุปวันทำงานของพนักงาน",
									Sorting = 2,
									Active = 1,
									Url = Leave.Properties.Settings.Default.LinkReportWorkday
								},*/
                                new ItemSubRecord()
								{
									NameTH = "พนักงานที่ยังไม่บันทึกชื่ออีเมลล์ในระบบ",
									Sorting = 3,
									Active = 1,
									Url = Leave.Properties.Settings.Default.LinkReportNoEmail
								}
							}
						},
						#endregion
						#region 5. อื่นๆ
						new ItemRecord()
						{
							NameTH = "อื่นๆ",
							Sorting = 5,
							Active = 1,
							Roles = new List<RoleRecord>()
							{
								new RoleRecord { NameTH = Const.ROLE_MY }
							},
							ItemsSub = new List<ItemSubRecord>()
							{/*
								new ItemSubRecord()
								{
									NameTH = "ระเบียบบริษัท",
									Sorting = 1,
									Active = 1,
									Url = Url.Content("~/Registered/Policy")
								},*/
								new ItemSubRecord()
								{
									NameTH = "ปฏิทินวันหยุด",
									Sorting = 2,
									Active = 1,
									Url = Url.Content("~/Registered/Holiday")
								}/*,
								new ItemSubRecord()
								{
									NameTH = "คู่มือการใช้งาน",
									Sorting = 3,
									Active = 1,
									Url = Url.Content("~/Content/User Manual.pdf")
								}*/
							}
						}
						#endregion
					}
					#endregion
				}//,
				//new MenuSuiteRecord() // Time Attendance
				//{
				//    MenuID = 2,
				//    NameTH = "ระบบตรวจสอบเวลาทำงาน",
				//    Sorting = 2,
				//    Active = 1,
				//    #region Menu Items
				//    Items = new List<ItemRecord>()
				//    {
				//        #region 1. พนักงาน
				//        new ItemRecord()
				//        {
				//            NameTH = "พนักงาน",
				//            Sorting = 1,
				//            Active = 1,
				//            Roles = new List<RoleRecord>()
				//            {
				//                new RoleRecord { NameTH = Const.ROLE_MY }
				//            },
				//            ItemsSub = new List<ItemSubRecord>()
				//            {
				//                new ItemSubRecord()
				//                {
				//                    NameTH = "สถิติขาด ลา มาสาย",
				//                    Sorting = 1,
				//                    Active = 1,
				//                    Url = Url.Content("~/Attendance/Index")
				//                },
				//                new ItemSubRecord()
				//                {
				//                    NameTH = "สรุปเวลาทำงานรายวัน",
				//                    Sorting = 1,
				//                    Active = 1,
				//                    Url = Url.Content("~/Attendance/Daily")
				//                }
				//            }
				//        },
				//        #endregion
				//        #region 2. Manage Finger
				//        new ItemRecord()
				//        {
				//            NameTH = "เครื่องสแกนนิ้ว",
				//            Sorting = 2,
				//            Active = 1,
				//            Roles = new List<RoleRecord>()
				//            {
				//                new RoleRecord { NameTH = Const.ROLE_HR }
				//            },
				//            ItemsSub = new List<ItemSubRecord>()
				//            {
				//                new ItemSubRecord()
				//                {
				//                    NameTH = "ตั้งค่ารหัสสแกนนิ้วมือ",
				//                    Sorting = 1,
				//                    Active = 1,
				//                    Url = Url.Content("~/Finger/Setting")
				//                },
				//                new ItemSubRecord()
				//                {
				//                    NameTH = "รายชื่อพนักงานยกเว้น",
				//                    Sorting = 1,
				//                    Active = 1,
				//                    Url = Url.Content("~/Finger/Exception")
				//                },
				//                new ItemSubRecord()
				//                {
				//                    NameTH = "ดึงข้อมูลเวลาเข้า-ออกงาน",
				//                    Sorting = 2,
				//                    Active = 1,
				//                    Url = Url.Content("~/Finger/Getting")
				//                }
				//            }
				//        },
				//        #endregion
				//        #region 3. จัดการข้อมูล
				//        new ItemRecord()
				//        {
				//            NameTH = "จัดการข้อมูลเวลาทำงาน",
				//            Sorting = 3,
				//            Active = 1,
				//            Roles = new List<RoleRecord>()
				//            {
				//                new RoleRecord { NameTH = Const.ROLE_HR }
				//            },
				//            ItemsSub = new List<ItemSubRecord>()
				//            {
				//                new ItemSubRecord()
				//                {
				//                    NameTH = "แก้ไขเวลาเข้า-ออกงาน",
				//                    Sorting = 3,
				//                    Active = 1,
				//                    Url = Url.Content("~/Attendance/Adjusting")
				//                },
				//                new ItemSubRecord()
				//                {
				//                    NameTH = "คำนวณเวลาทำงาน",
				//                    Sorting = 1,
				//                    Active = 1,
				//                    Url = Url.Content("~/Attendance/Consolidate")
				//                },
				//                new ItemSubRecord()
				//                {
				//                    NameTH = "ตรวจสอบเวลาทำงาน",
				//                    Sorting = 1,
				//                    Active = 1,
				//                    Url = Url.Content("~/Attendance/Rechecked")
				//                },
				//                new ItemSubRecord()
				//                {
				//                    NameTH = "ส่งอีเมลล์สรุปประจำเดือน",
				//                    Sorting = 2,
				//                    Active = 1,
				//                    Url = Url.Content("~/Attendance/Mailing")
				//                }
				//            }
				//        }
				//        #endregion
				//    }
				//    #endregion
				//}
			};
			return Records;
		}
		
		public class RoleRecord
		{
			public int RoleID { get; set; }
			public string NameTH { get; set; }
			public string NameEN { get; set; }
		}
		public class ItemRecord
		{
			public int ItemID { get; set; }
			public List<RoleRecord> Roles { get; set; }
			public List<ItemSubRecord> ItemsSub { get; set; }
			public string NameTH { get; set; }
			public string NameEN { get; set; }
			public int Sorting { get; set; }
			public int Active { get; set; }
			public string Url { get; set; }
			public string Target { get; set; }
		}
		public class ItemSubRecord
		{
			public int ItemSubID { get; set; }
			public string NameTH { get; set; }
			public string NameEN { get; set; }
			public int Type { get; set; }
			public int Sorting { get; set; }
			public int Active { get; set; }
			public string Url { get; set; }
			public string Target { get; set; }
		}
		public class MenuSuiteRecord
		{
			public int MenuID { get; set; }
			public string NameTH { get; set; }
			public string NameEN { get; set; }
			public int Sorting { get; set; }
			public int Active { get; set; }
			public List<ItemRecord> Items { get; set; }
		}
    }
}
