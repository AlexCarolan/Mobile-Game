using System.Collections.Generic;

namespace Unity.Services.Analytics.Internal
{
    interface IBuffer
    {
        void PushStandardEventStart(string name, int version);
        void PushCustomEventStart(string name);
        void PushEndEvent();

        void PushDouble(string name, double value);
        void PushFloat(string name, float value);
        void PushString(string name, string value);
        void PushInt64(string name, long value);
        void PushInt(string name, int value);
        void PushBool(string name, bool value);
        void PushObject(string name, object value);
        void PushProduct(string name, TransactionRealCurrency realCurrency, List<TransactionVirtualCurrency> virtualCurrencies, List<TransactionItem> items);

        void FlushToDisk();
        void ClearDiskCache();
        void LoadFromDisk();

        int Length { get; }
        byte[] Serialize();
        void ClearBuffer();
        void ClearBuffer(long upTo);
    }
}
