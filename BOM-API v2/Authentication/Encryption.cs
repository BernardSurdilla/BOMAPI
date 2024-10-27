using Microsoft.AspNetCore.Identity;
using System.Security.Cryptography;
using System.Text;

namespace BOM_API_v2.Authentication
{
    public class UserDataLookupProtectorKeyRing : ILookupProtectorKeyRing
    {
        public string this[string keyId]
        {
            get
            {
                return GetAllKeyIds().Where(x => x == keyId).FirstOrDefault();
            }
        }

        public string CurrentKeyId
        {
            get
            {
                byte[] key = { 89, 231, 180, 100, 205, 33, 147, 220, 52, 66, 174, 123, 187, 240, 44, 90 };
                var currentKey = Convert.ToBase64String(key);
                return currentKey;
            }
        }

        public IEnumerable<string> GetAllKeyIds()
        {
            var list = new List<string>();
            byte[] key = { 89, 231, 180, 100, 205, 33, 147, 220, 52, 66, 174, 123, 187, 240, 44, 90 };
            byte[] key2 = { 150, 27, 99, 205, 64, 158, 213, 39, 117, 72, 15, 198, 221, 66, 132, 210 };
            byte[] key3 = { 201, 18, 147, 81, 29, 176, 45, 213, 110, 88, 130, 199, 140, 210, 125, 60 };

            list.Add(Convert.ToBase64String(key));
            list.Add(Convert.ToBase64String(key2));
            list.Add(Convert.ToBase64String(key3));


            return list;
        }
    }

    public class UserDataLookupProtector : ILookupProtector
    {

        readonly byte[] iv = { 125, 43, 200, 9, 173, 80, 145, 67, 213, 34, 112, 147, 201, 19, 178, 230 };
        public string Protect(string keyId, string data)
        {
            byte[] plainTextBytes = Encoding.UTF8.GetBytes(data);

            string cipherText;
            using (SymmetricAlgorithm algorithm = Aes.Create())
            {
                using (ICryptoTransform encryptor = algorithm.CreateEncryptor(Encoding.UTF8.GetBytes(keyId), iv))
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        using (CryptoStream cryptoStream = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                        {
                            cryptoStream.Write(plainTextBytes, 0, plainTextBytes.Length);
                            cryptoStream.Close();
                            byte[] chiperTextByte = ms.ToArray();
                            cipherText = Convert.ToBase64String(chiperTextByte);
                        }
                    }
                }
            }

            return cipherText;
        }


        public string Unprotect(string keyId, string data)
        {
            byte[] cipherTextBytes = Convert.FromBase64String(data);
            string plainText;
            using (SymmetricAlgorithm algorithm = Aes.Create())
            {
                using (ICryptoTransform decrypter = algorithm.CreateDecryptor(Encoding.UTF8.GetBytes(keyId), iv))
                {
                    using (MemoryStream ms = new MemoryStream(cipherTextBytes))
                    {
                        using (CryptoStream cryptoStream = new CryptoStream(ms, decrypter, CryptoStreamMode.Read))
                        {
                            using (StreamReader streamReader = new StreamReader(cryptoStream))
                            {
                                plainText = streamReader.ReadToEnd();
                            }
                        }
                    }
                }
            }

            return plainText;
        }
    }
}
