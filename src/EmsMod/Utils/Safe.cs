using System;

namespace EmsMod.Utils
{
    public static class Safe
    {
        public static void Run(Action action, string context)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Log.Error(ex, context);
            }
        }

        public static T Run<T>(Func<T> func, T fallback, string context)
        {
            try
            {
                return func();
            }
            catch (Exception ex)
            {
                Log.Error(ex, context);
                return fallback;
            }
        }
    }
}
