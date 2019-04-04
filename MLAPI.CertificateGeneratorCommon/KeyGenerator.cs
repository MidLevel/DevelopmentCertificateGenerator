using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Threading;

namespace MLAPI.CertificateGeneratorCommon
{
    public class PendingRSAKey
    {
        public RSAParameters Key;
        public DateTime CreationTime;
    }
    
    public static class KeyGenerator
    {
        private static Thread _thread = null;
        private static bool _isRunning = false;
        private static int _keySize;
        private static ConcurrentQueue<PendingRSAKey> _generatedKeys = new ConcurrentQueue<PendingRSAKey>();
        
        public static void Start(int size)
        {
            if (_isRunning)
            {
                throw new Exception("Already running");
            }
            _keySize = size;
            _isRunning = true;
            _thread = new Thread(Run);
            _thread.Start();
        }

        private static void Run()
        {
            while (_isRunning)
            {
                while (_generatedKeys.Count >= 20 && _isRunning)
                {
                    RotateKeys();
                    Thread.Sleep(100);
                }

                if (!_isRunning)
                    break;

                _generatedKeys.Enqueue(new PendingRSAKey()
                {
                    Key = GenerateKey(),
                    CreationTime = DateTime.UtcNow
                });
                
                Console.WriteLine("Generated key. Queue size: " + _generatedKeys.Count);
            }
        }

        private static void RotateKeys()
        {
            if (_generatedKeys.TryPeek(out PendingRSAKey oldestKey))
            {
                if ((DateTime.UtcNow - oldestKey.CreationTime).TotalMinutes > 10)
                {
                    _generatedKeys.TryDequeue(out PendingRSAKey dequeuedKey);
                }
            }
        }

        public static RSAParameters Get()
        {
            PendingRSAKey rsa;
            
            while (!_generatedKeys.TryDequeue(out rsa)) 
                Thread.SpinWait(1);

            return rsa.Key;
        }

        private static RSAParameters GenerateKey()
        {
            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(_keySize))
            {
                try
                {
                    RSAParameters parameters = rsa.ExportParameters(true);
                    
                    return parameters;
                }
                finally
                {
                    rsa.PersistKeyInCsp = false;
                }
            }
        }
    }
}