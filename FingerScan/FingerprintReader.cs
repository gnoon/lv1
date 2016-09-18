using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FingerScan.Interface
{
    public class FingerprintReader : IDisposable
    {
        private IFingerprintDataSource _Scanner;

        public FingerprintReader(IFingerprintDataSource Scanner)
        {
            _Scanner = Scanner;
        }

        public RawRecord Current { get { return _Current; } }
        private RawRecord _Current;

        public bool Read()
        {
            if (!_Scanner.DatalinkMode)
            {
                _Scanner.DatalinkMode = true;
                if (!_Scanner.ReadToEnd())
                {
                    int error = _Scanner.LastError;
                    if (error == 0) // No data from terminal returns!
                        return false;
                    else
                        throw new FScannerException(error.ToString());
                }
            }
            _Current = _Scanner.Next;
            return (_Current != null);
        }

        #region IDisposable Members

        public void Dispose()
        {
            if (_Scanner != null)
                _Scanner.DatalinkMode = false;
        }

        #endregion
    }
}
