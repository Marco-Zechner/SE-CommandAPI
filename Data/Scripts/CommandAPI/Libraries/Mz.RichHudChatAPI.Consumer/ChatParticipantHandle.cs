using System;
using System.Collections.Generic;

namespace Mz.RichHudChatApi
{
    public sealed class ChatParticipantHandle :
        IDisposable
    {
        private RichHudChatApiClient _owner;

        private readonly List<ChatRouteHandle> _routes =
            new List<ChatRouteHandle>();

        internal string ProviderRegistrationId { get; set; }

        internal Action ProviderUnregister { get; set; }

        public ChatParticipantRegistration Registration { get; }

        public bool IsActive =>
            ProviderRegistrationId != null
            && ProviderUnregister != null;

        public bool IsDisposed { get; private set; }

        public Exception LastError { get; internal set; }

        internal ChatParticipantHandle(
            RichHudChatApiClient owner,
            ChatParticipantRegistration registration
        )
        {
            if (owner == null)
                throw new ArgumentNullException(nameof(owner));

            if (registration == null)
                throw new ArgumentNullException(nameof(registration));

            _owner = owner;
            Registration = registration;
        }

        public ChatRouteHandle RegisterRoute(
            ChatRouteRegistration registration,
            Action<string> submitHandler,
            Action<string> inputChangedHandler = null,
            Action<string> interactionHandler = null
        )
        {
            ThrowIfDisposed();

            if (registration == null)
                throw new ArgumentNullException(nameof(registration));

            if (submitHandler == null)
                throw new ArgumentNullException(nameof(submitHandler));

            var handle =
                new ChatRouteHandle(
                    this,
                    registration,
                    submitHandler,
                    inputChangedHandler,
                    interactionHandler
                );

            _routes.Add(handle);

            if (IsActive)
            {
                try
                {
                    _owner.ActivateRoute(
                        this,
                        handle
                    );
                }
                catch
                {
                    _routes.Remove(handle);
                    handle.MarkDisposed();
                    throw;
                }
            }

            return handle;
        }

        public bool AppendTranscript(
            string message,
            string author = null
        )
        {
            ThrowIfDisposed();

            return _owner.AppendTranscript(
                this,
                author,
                message
            );
        }

        public void Dispose()
        {
            if (IsDisposed)
                return;

            RichHudChatApiClient owner =
                _owner;

            if (owner != null)
                owner.RemoveParticipant(this);
            else
                MarkDisposed();
        }

        internal ChatRouteHandle[] GetRoutes()
        {
            return _routes.ToArray();
        }

        internal bool SetCompanion(
            ChatRouteHandle route,
            ChatCompanionState state
        )
        {
            return _owner.SetCompanion(
                this,
                route,
                state
            );
        }

        internal bool ClearCompanion(
            ChatRouteHandle route
        )
        {
            return _owner.ClearCompanion(
                this,
                route
            );
        }

        internal bool SetInput(
            ChatRouteHandle route,
            string input
        )
        {
            return _owner.SetInput(
                this,
                route,
                input
            );
        }

        internal void RemoveRoute(
            ChatRouteHandle handle
        )
        {
            if (
                handle == null
                || handle.IsDisposed
            )
            {
                return;
            }

            _routes.Remove(handle);
            handle.MarkDisposed();
        }

        internal void ReleaseProviderRegistration()
        {
            ChatRouteHandle[] routes =
                _routes.ToArray();

            for (
                int index = 0;
                index < routes.Length;
                index++
            )
            {
                routes[index]
                    .ReleaseProviderRegistration();
            }

            Action unregister =
                ProviderUnregister;

            ProviderRegistrationId = null;
            ProviderUnregister = null;

            TryInvoke(unregister);
        }

        internal void MarkDisposed()
        {
            ReleaseProviderRegistration();

            ChatRouteHandle[] routes =
                _routes.ToArray();

            _routes.Clear();

            for (
                int index = 0;
                index < routes.Length;
                index++
            )
            {
                routes[index].MarkDisposed();
            }

            _owner = null;
            IsDisposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new InvalidOperationException(
                    "The chat participant registration has been disposed."
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