using System;

namespace LeaveCore
{
    public interface IIdentifiable
    {
        string InstanceID { get; }
    }

    /// <summary>
    /// ใช้ในการระบุ Instance ID ในฟังก์ชันการส่งเมลล์เพื่อค้นหา Record ใน Database
    /// </summary>
    public class IdentifiableObject : IIdentifiable
    {
        #region IIdentifyable Members

        string _InstanceID;
        public string InstanceID
        {
            get { if (_InstanceID == null) _InstanceID = Guid.NewGuid().ToString(); return _InstanceID; }
        }

        #endregion
    }
}
