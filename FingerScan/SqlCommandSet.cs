using System;
using System.Data.SqlClient;
using System.Reflection;

namespace FingerScan.Data
{
    public sealed class SqlCommandSet : IDisposable
    {
        private static readonly Type commandSetType;
        private readonly object commandSet;
        private readonly Action<SqlCommand> appenderDel;
        private readonly Action disposeDel;
        private readonly Func<int> executeNonQueryDel;
        private readonly Func<SqlConnection> connectionGetDel;
        private readonly Action<SqlConnection> connectionSetDel;
        private readonly Action<SqlTransaction> transactionSetDel;

        private int commandCount;

        static SqlCommandSet()
        {
            Assembly systemData = Assembly.Load("System.Data, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
            commandSetType = systemData.GetType("System.Data.SqlClient.SqlCommandSet");
        }

        public SqlCommandSet()
        {
            commandSet = Activator.CreateInstance(commandSetType, true);
            appenderDel = (Action<SqlCommand>)Delegate.CreateDelegate(typeof(Action<SqlCommand>), commandSet, "Append");
            disposeDel = (Action)Delegate.CreateDelegate(typeof(Action), commandSet, "Dispose");
            executeNonQueryDel = (Func<int>)Delegate.CreateDelegate(typeof(Func<int>), commandSet, "ExecuteNonQuery");
            connectionGetDel = (Func<SqlConnection>)Delegate.CreateDelegate(typeof(Func<SqlConnection>), commandSet, "get_Connection");
            connectionSetDel = (Action<SqlConnection>)Delegate.CreateDelegate(typeof(Action<SqlConnection>), commandSet, "set_Connection");
            transactionSetDel = (Action<SqlTransaction>)Delegate.CreateDelegate(typeof(Action<SqlTransaction>), commandSet, "set_Transaction");
        }

        public void Append(SqlCommand command)
        {
            commandCount++;
            appenderDel.Invoke(command);
        }

        public int ExecuteNonQuery()
        {
            return executeNonQueryDel.Invoke();
        }

        public SqlConnection Connection
        {
            get
            {
                return connectionGetDel.Invoke();
            }
            set
            {
                connectionSetDel.Invoke(value);
            }
        }

        public SqlTransaction Transaction
        {
            set
            {
                transactionSetDel.Invoke(value);
            }
        }

        public int CommandCount
        {
            get
            {
                return commandCount;
            }
        }

        public void Dispose()
        {
            disposeDel.Invoke();
        }
    }
}
