using System;
using System.Collections.Generic;
using UnityEngine;

namespace DazPose.Performer
{
    public enum SpeechCompletion
    {
        Finished,
        Cancelled,
        PerformerDisabled
    }

    internal sealed class PerformerSpeech : IDisposable
    {
        private readonly AudioSource _audioSource;
        private readonly Queue<SpeechRequest> _pending = new Queue<SpeechRequest>();
        private SpeechRequest _current;
        private long _nextRequestId;
        private int _startedRequestCount;
        private bool _hasObservedPlayback;
        private bool _reportedSourceInterference;
        private bool _disposed;

        public PerformerSpeech(AudioSource audioSource)
        {
            _audioSource = audioSource != null
                ? audioSource
                : throw new ArgumentNullException(nameof(audioSource));
            if (!_audioSource.isActiveAndEnabled)
                throw new InvalidOperationException("The dedicated speech AudioSource must be active and enabled when the performer runtime starts.");

            _audioSource.loop = false;
            _audioSource.playOnAwake = false;
        }

        public bool IsSpeaking => _current != null;
        public AudioClip CurrentSpeechClip => _current == null ? null : _current.Clip;
        public int PendingSpeechCount => _pending.Count;

        internal long CurrentRequestId => _current == null ? 0 : _current.Id;
        internal int StartedRequestCount => _startedRequestCount;

        public void Say(AudioClip clip)
        {
            Enqueue(clip, null);
        }

        public Awaitable<SpeechCompletion> SayAsync(AudioClip clip)
        {
            if (clip == null) throw new ArgumentNullException(nameof(clip));
            var completion = new AwaitableCompletionSource<SpeechCompletion>();
            Enqueue(clip, completion);
            return completion.Awaitable;
        }

        public void StopSpeaking()
        {
            if (_disposed) return;

            var cancelled = TakeAllRequests();
            StopAndClearAudioSource();
            Complete(cancelled, SpeechCompletion.Cancelled);
        }

        public void Advance()
        {
            if (_disposed) return;
            AdvancePlaybackState(_audioSource.isPlaying);
        }

        internal void AdvanceForAcceptance(bool sourceIsPlaying)
        {
            if (_disposed) return;
            AdvancePlaybackState(sourceIsPlaying);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            var disabled = TakeAllRequests();
            StopAndClearAudioSource();
            Complete(disabled, SpeechCompletion.PerformerDisabled);
        }

        private void Enqueue(AudioClip clip, AwaitableCompletionSource<SpeechCompletion> completion)
        {
            if (clip == null) throw new ArgumentNullException(nameof(clip));
            if (_disposed)
                throw new InvalidOperationException("The performer speech runtime is no longer active.");
            if (!_audioSource.isActiveAndEnabled)
                throw new InvalidOperationException("The dedicated speech AudioSource must be active and enabled to enqueue speech.");

            var request = new SpeechRequest(++_nextRequestId, clip);
            if (completion != null) request.Waiters.Add(completion);
            _pending.Enqueue(request);
            StartNextIfIdle();
        }

        private void AdvancePlaybackState(bool sourceIsPlaying)
        {
            if (_current == null)
            {
                StartNextIfIdle();
                return;
            }

            if (_audioSource.clip != _current.Clip)
            {
                if (!_reportedSourceInterference)
                {
                    _reportedSourceInterference = true;
                    Debug.LogError("The dedicated speech AudioSource clip was changed outside PerformerSpeech. Speech remains owned by the performer; call StopSpeaking to clear it.", _audioSource);
                }
                return;
            }

            if (sourceIsPlaying)
            {
                _hasObservedPlayback = true;
                return;
            }

            if (!_hasObservedPlayback) return;
            FinishCurrent();
        }

        private void StartNextIfIdle()
        {
            if (_disposed || _current != null || _pending.Count == 0) return;

            _current = _pending.Dequeue();
            _hasObservedPlayback = false;
            _reportedSourceInterference = false;
            _audioSource.clip = _current.Clip;
            _audioSource.Play();
            _startedRequestCount++;
            if (_audioSource.isPlaying) _hasObservedPlayback = true;
        }

        private void FinishCurrent()
        {
            var finished = _current;
            _current = null;
            _hasObservedPlayback = false;
            _reportedSourceInterference = false;

            if (_audioSource.clip == finished.Clip) _audioSource.clip = null;
            StartNextIfIdle();

            // Starting the next queued line before resuming this line's waiters keeps
            // pre-existing FIFO requests ahead of anything enqueued reentrantly.
            Complete(finished, SpeechCompletion.Finished);
        }

        private List<SpeechRequest> TakeAllRequests()
        {
            var requests = new List<SpeechRequest>(_pending.Count + (_current == null ? 0 : 1));
            if (_current != null) requests.Add(_current);
            _current = null;
            _hasObservedPlayback = false;
            _reportedSourceInterference = false;
            while (_pending.Count > 0) requests.Add(_pending.Dequeue());
            return requests;
        }

        private void StopAndClearAudioSource()
        {
            if (_audioSource == null) return;
            _audioSource.Stop();
            _audioSource.clip = null;
        }

        private static void Complete(IEnumerable<SpeechRequest> requests, SpeechCompletion result)
        {
            foreach (var request in requests) Complete(request, result);
        }

        private static void Complete(SpeechRequest request, SpeechCompletion result)
        {
            if (request == null) return;
            var waiters = request.Waiters.ToArray();
            request.Waiters.Clear();
            foreach (var waiter in waiters) waiter.TrySetResult(result);
        }

        private sealed class SpeechRequest
        {
            public SpeechRequest(long id, AudioClip clip)
            {
                Id = id;
                Clip = clip;
            }

            public long Id { get; }
            public AudioClip Clip { get; }
            public List<AwaitableCompletionSource<SpeechCompletion>> Waiters { get; }
                = new List<AwaitableCompletionSource<SpeechCompletion>>();
        }
    }
}
