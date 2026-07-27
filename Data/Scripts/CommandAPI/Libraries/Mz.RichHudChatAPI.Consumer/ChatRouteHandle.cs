using System;

namespace Mz.RichHudChatApi
{
    public sealed class ChatRouteHandle :
        IDisposable
    {
        private ChatParticipantHandle _participant;

        internal Action<string> SubmitHandler { get; }

        internal Action<string> InputChangedHandler { get; }

        internal Action<string> InteractionHandler { get; }

        internal Action ProviderUnregisterRoute { get; set; }

        internal Action ProviderUnregisterInteraction { get; set; }

        public ChatRouteRegistration Registration { get; }

        public bool IsActive =>
            ProviderUnregisterRoute != null;

        public bool IsDisposed { get; private set; }

        public Exception LastError { get; internal set; }

        internal ChatRouteHandle(
            ChatParticipantHandle participant,
            ChatRouteRegistration registration,
            Action<string> submitHandler,
            Action<string> inputChangedHandler,
            Action<string> interactionHandler
        )
        {
            if (participant == null)
                throw new ArgumentNullException(nameof(participant));

            if (registration == null)
                throw new ArgumentNullException(nameof(registration));

            if (submitHandler == null)
                throw new ArgumentNullException(nameof(submitHandler));

            _participant = participant;
            Registration = registration;
            SubmitHandler = submitHandler;
            InputChangedHandler = inputChangedHandler;
            InteractionHandler = interactionHandler;
        }

        public bool SetCompanion(
            ChatCompanionState state
        )
        {
            ThrowIfDisposed();

            return _participant.SetCompanion(
                this,
                state
            );
        }

        public bool ClearCompanion()
        {
            ThrowIfDisposed();

            return _participant.ClearCompanion(
                this
            );
        }

        public bool SetInput(
            string input
        )
        {
            ThrowIfDisposed();

            return _participant.SetInput(
                this,
                input
            );
        }

        public void Dispose()
        {
            if (IsDisposed)
                return;

            ChatParticipantHandle participant =
                _participant;

            if (participant != null)
                participant.RemoveRoute(this);
            else
                MarkDisposed();
        }

        internal void ReleaseProviderRegistration()
        {
            Action unregisterInteraction =
                ProviderUnregisterInteraction;

            Action unregisterRoute =
                ProviderUnregisterRoute;

            ProviderUnregisterInteraction = null;
            ProviderUnregisterRoute = null;

            TryInvoke(unregisterInteraction);
            TryInvoke(unregisterRoute);
        }

        internal void MarkDisposed()
        {
            ReleaseProviderRegistration();
            _participant = null;
            IsDisposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new InvalidOperationException(
                    "The chat route registration has been disposed."
                );
            }
        }

        private static void TryInvoke(
            Action action
        )
        {
            if (action == null)
                return;

            try
            {
                action();
            }
            catch
            {
            }
        }
    }
}