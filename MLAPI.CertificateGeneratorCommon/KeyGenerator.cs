using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace MLAPI.CertificateGeneratorCommon
{
    public class PendingRSAKey
    {
        public RSAParameters Key;
        public DateTime CreationTime;
    }
    
    public static class KeyGenerator
    {
        private static bool _isRunning = false;
        private static int _keySize;
        private static int _queueSize;
        
        private static readonly ReaderWriterLockSlim _queueLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        private static readonly Queue<PendingRSAKey> _generatedKeys = new Queue<PendingRSAKey>();
        
        public static void Start(int keySize, int threadCount, int queueSize)
        {
            if (_isRunning)
            {
                throw new Exception("Already running");
            }
            _keySize = keySize;
            _isRunning = true;
            _queueSize = queueSize;

            for (int i = 0; i < threadCount; i++)
            {
                Task.Factory.StartNew(Run);
            }
        }

        private static void Run()
        {
            while (_isRunning)
            {
                while (_generatedKeys.Count >= _queueSize && _isRunning)
                {
                    RotateKeys();
                    Thread.Sleep(100);
                }

                if (!_isRunning)
                    break;

                PendingRSAKey newKey = new PendingRSAKey()
                {
                    Key = GenerateKey(),
                    CreationTime = DateTime.UtcNow
                };

                _queueLock.EnterWriteLock();

                try
                {
                    _generatedKeys.Enqueue(newKey);
                }
                finally
                {
                    _queueLock.ExitWriteLock();
                }
                
                Console.WriteLine("Generated key. Queue size: " + _generatedKeys.Count);
            }
        }

        private static void RotateKeys()
        {
            _queueLock.EnterUpgradeableReadLock();

            try
            {
                if (_generatedKeys.Count > _queueSize)
                {
                    _queueLock.EnterWriteLock();

                    try
                    {
                        _generatedKeys.Dequeue();
                    }
                    finally
                    {
                        _queueLock.ExitWriteLock();
                    }

                    Console.WriteLine("Rotated key for being too many...");
                }

                if (_generatedKeys.Count > 0)
                {
                    PendingRSAKey oldestKey = _generatedKeys.Peek();

                    if ((DateTime.UtcNow - oldestKey.CreationTime).TotalMinutes > 10)
                    {
                        _queueLock.EnterWriteLock();
                        
                        try
                        {
                            _generatedKeys.Dequeue();
                        }
                        finally
                        {
                            _queueLock.ExitWriteLock();
                        }
                        Console.WriteLine("Rotated key for being too old...");
                    }
                }
            }
            finally
            {
                _queueLock.ExitUpgradeableReadLock();
            }
        }

        public static RSAParameters Get()
        {
            PendingRSAKey rsa = null;

            do
            {
                _queueLock.EnterUpgradeableReadLock();
                
                try
                {
                    if (_generatedKeys.Count > 0)
                    {
                        _queueLock.EnterWriteLock();

                        try
                        {
                            rsa = _generatedKeys.Dequeue();
                        }
                        finally
                        {
                            _queueLock.ExitWriteLock();
                        }
                    }
                }
                finally
                {
                    _queueLock.ExitUpgradeableReadLock();
                }

                if (rsa == null)
                {
                    Thread.SpinWait(5);
                }
            } while (rsa == null);
            
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