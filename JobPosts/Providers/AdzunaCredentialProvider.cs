using JobPosts.Models;
using JobPosts.Options;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Linq;

namespace JobPosts.Providers
{
    public class AdzunaCredentialProvider : IAdzunaCredentialProvider
    {
        private readonly List<AdzunaCredential> _credentials;
        private int _currentIndex = 0;
        private readonly HashSet<int> _exhaustedIndices = new();

        public AdzunaCredentialProvider(IOptions<AdzunaOptions> options)
        {
            _credentials = options.Value.Credentials ?? new List<AdzunaCredential>();
        }

        public AdzunaCredential? GetNextCredential()
        {
            if (_credentials.Count == 0) return null;
            int tries = 0;
            while (tries < _credentials.Count)
            {
                if (!_exhaustedIndices.Contains(_currentIndex))
                {
                    var cred = _credentials[_currentIndex];
                    _currentIndex = (_currentIndex + 1) % _credentials.Count;
                    return cred;
                }
                _currentIndex = (_currentIndex + 1) % _credentials.Count;
                tries++;
            }
            return null;
        }

        public void MarkCredentialAsExhausted(AdzunaCredential credential)
        {
            int idx = _credentials.FindIndex(c => c.AppId == credential.AppId && c.AppKey == credential.AppKey);
            if (idx >= 0)
                _exhaustedIndices.Add(idx);
        }

        public void ResetExhausted()
        {
            _exhaustedIndices.Clear();
        }
    }
} 