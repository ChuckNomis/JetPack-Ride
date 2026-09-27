using UnityEngine;

namespace JetpackRide.Core
{
    public static class AwaitableExtensions
    {
        public static async void Forget(this Awaitable awaitable)
        {
            try { await awaitable; }
            catch (System.OperationCanceledException) { }
        }
    }
}
