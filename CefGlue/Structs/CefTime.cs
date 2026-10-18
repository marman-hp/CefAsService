namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Text;

    using Xilium.CefGlue.Interop;

    [StructLayout(LayoutKind.Sequential, Pack = libcef.ALIGN)]
    public struct CefTime
    {
        private static readonly DateTime s_maxDateTime = new DateTime(DateTime.MaxValue.Ticks, DateTimeKind.Utc);

        public int Year;

        public int Month;

        public int DayOfWeek;

        public int DayOfMonth;

        public int Hour;

        public int Minute;

        public int Second;

        public int Millisecond;

        public CefTime(DateTime value)
        {
            value = value.ToUniversalTime();

            Year = value.Year;
            Month = value.Month;
            DayOfWeek = (int)value.DayOfWeek;
            DayOfMonth = value.Day;
            Hour = value.Hour;
            Minute = value.Minute;
            Second = value.Second;
            Millisecond = value.Millisecond;
        }

        public DateTime ToDateTime()
        {
            if (Year > 9999) return s_maxDateTime;
            return new DateTime(
                Year,
                Month,
                DayOfMonth,
                Hour,
                Minute,
                Second != 60 ? Second : 59,
                Millisecond,
                DateTimeKind.Utc
                );
        }

        public static unsafe DateTime ToDateTime(CefTime* ptr)
        {
            var year = ptr->Year;
            if (year > 9999) return s_maxDateTime;
            return new DateTime(
                year,
                ptr->Month,
                ptr->DayOfMonth,
                ptr->Hour,
                ptr->Minute,
                ptr->Second != 60 ? ptr->Second : 59,
                ptr->Millisecond,
                DateTimeKind.Utc
                );
        }
    }
}
