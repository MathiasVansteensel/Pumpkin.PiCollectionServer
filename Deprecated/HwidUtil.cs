using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Pumpkin.PiCollectionServer;
[Obsolete("Just use Guid class like a normal person :)")]
internal static class HwidUtil
{
	//lighter hashing algo i made myself (which u can prop see :) ) same as i used on arduinos
	//i am aware that this is a bad translation but i originally wrote it in cpp and i'm lazy so it's a direct translation without too much thought, computers are fast enough to use sha256 or smth
	public static string LightHash(string str1, string str2)
	{
		int length1 = str1.Length;
		int length2 = str2.Length;
		ulong hash1 = 1;
		ulong hash2 = 1;

		for (byte i = 0; i < length1; i++)
		{
			byte character = (byte)str1[i];
			if (character % 2 == 0) hash1 *= (ulong)(character | str1[(int)hash1 % length1]) * i; // even
			else hash1 ^= (ulong)i * str1[(int)hash2 % length1]; // odd
		}

		for (byte i = 0; i < length2; i++)
		{
			byte character = (byte)str2[i];
			if (character % 2 == 0) hash2 ^= (ulong)i * (byte)str1[(int)(hash2) % length2]; // even
			else hash2 *= (ulong)(character | (byte)str1[(int)(hash2) % length2]) * i; // odd
		}

		hash1 %= 0xFFFFFFFF;
		hash2 = ~hash2 % 0xFFFFFFFF;
		ulong resultHash = ~(hash1 + hash2);

		// First element is useless because I add 1 to the maskedByte thingy which means it can never be 0
		char[] allowedCharacters = {
			' ', '1', '6', 'O', '}', '7', 'S', 'y', '3', 'z', 'U', '9', 'i', 'p', 'Q', 'C', '%', '5', 'F',
			'}', 'L', 'Y', '2', 'N', '@', 'g', 'A', 'H', 'M', '0', 'j', 'e', '#', 'B', 'x', '4', '8',
			'D', 'I', 'c', 'o', 'h', 'G', '-', 'P', 'K', 's', '&', 'v', 'a', 'b', 'W',
			'r', 'f', '_', 'Z', 'n', 'q', 'R', 'X', 'k', 'd', 't', '}', 'J', 'E', 'l', 'Q',
			':'
		};

		char[] outputBuffer = new char[11];
		byte j = 0;
		while (resultHash != 0)
		{
			byte maskedByte = (byte)(((resultHash % 100) + 1) / 1.470588235294118); // get every pair of digits and map to index for 68 (+1) elements
			byte index = (byte)Math.Round((double)maskedByte);
			outputBuffer[j++] = allowedCharacters[index != 0 ? index : index + 1]; // should never be 0 but somehow it is sometimes :(
			resultHash /= 100;
		}

		return new string(outputBuffer);
	}

	public static string HashSha256(string input)
	{
		using (SHA256 sha256 = SHA256.Create())
		{
			byte[] hashBuffer = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));

			string resultHash = string.Empty;
			for (int i = 0; i < hashBuffer.Length; i++) resultHash += hashBuffer[i].ToString("x2");

			return resultHash;
		}
	}

	////works only on windows anyway
	//private static string GetHardwareInfo(string wmiClassName, string propertyName)
	//{
	//	using (ManagementObjectSearcher searcher = new ManagementObjectSearcher($"SELECT {propertyName} FROM {wmiClassName}"))
	//	{
	//		foreach (ManagementObject obj in searcher.Get())
	//		{
	//			object value = obj[propertyName];
	//			if (value != null)
	//			{
	//				return value.ToString();
	//			}
	//		}
	//	}

	//	return string.Empty;
	//}
}
