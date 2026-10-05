using System.Threading;
using UnityEngine;

namespace TravelPlanning.UI.Shared
{
    /// <summary>
    /// Owns the one current request for a screen. Cancelling increments a version number,
    /// so a completed older request cannot paint over a newer screen state.
    /// </summary>
    public abstract class ScreenControllerBase : MonoBehaviour
    {
        private CancellationTokenSource requestLifetime;
        protected int RequestRevision { get; private set; }

        protected CancellationToken BeginScreenRequest()
        {
            // Call CancelScreenRequest before replacing an existing request, then capture
            // RequestRevision. The initial startup request does not need cancellation.
            // Requests start in Play mode. Avoid asking Unity for its destruction token
            // during Inspector construction or Edit-mode component tests.
            requestLifetime = Application.isPlaying ? CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken) : new CancellationTokenSource();
            return requestLifetime.Token;
        }

        protected void CancelScreenRequest()
        {
            RequestRevision++;
            requestLifetime?.Cancel();
            requestLifetime?.Dispose();
            requestLifetime = null;
        }

        protected bool IsRequestCurrent(int revision) => this && revision == RequestRevision;
        protected virtual void OnDestroy()
        {
            CancelScreenRequest();
        }
    }
}
