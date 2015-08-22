﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.ComponentModel;

namespace FullO.Keygens.VisualSVN
{

	public class License : INotifyPropertyChanged
	{
		public Guid licenseId = Guid.NewGuid();
		public LicenseBinding Binding = LicenseBinding.Seat;
		private int capacity = 1;
		public DateTime EndTime = DateTime.MaxValue;
		private string licensedTo = "Your name";
		private DateTime purchaseDate = DateTime.Now;
		private string purchaseId = "Generated by [FullO]ptionite corp.";
		public DateTime StartTime = DateTime.MinValue;
		private LicenseType type = LicenseType.Corporate;
		public byte Version = 2;

		[DisplayName("Number of licenses"), Category("Options")]
		public int Capacity
		{
			get { return capacity; }
			set
			{
				if (Binding == LicenseBinding.Seat || (Binding == LicenseBinding.User && value == 1))
				{
					capacity = value;
				}
			}
		}

		[DisplayName("Licensed to"), Category("Options")]
		public string LicensedTo
		{
			get { return licensedTo; }
			set { licensedTo = value; }
		}

		[DisplayName("License id"), Category("Options")]
		public Guid LicenseId
		{
			get { return licenseId; }
		}

		[DisplayName("Purchase date"), Category("Options")]
		public DateTime PurchaseDate
		{
			get { return purchaseDate; }
			set { purchaseDate = value; }
		}

		[DisplayName("Purchase comment"), Category("Options")]
		public string PurchaseId
		{
			get { return purchaseId; }
			set { purchaseId = value; }
		}

		[DisplayName("License type"), Category("Options")]
		public LicenseType Type
		{
			get { return type; }
			set
			{
				type = value;
				if (type == LicenseType.Corporate || type == LicenseType.Classroom)
				{
					Binding = LicenseBinding.Seat;
				}
				else
				{
					Binding = LicenseBinding.User;
					capacity = 1;
				}

				if (PropertyChanged != null)
					PropertyChanged(this, new PropertyChangedEventArgs("Capacity"));
			}
		}

		public enum LicenseType
		{
			[Browsable(false)]
			Evaluation,
			Personal,
			Corporate,
			Classroom,
			OpenSource,
			Student
		}

		public enum LicenseBinding
		{
			User,
			Seat
		}

		#region INotifyPropertyChanged Members

		public event PropertyChangedEventHandler PropertyChanged;

		#endregion
	}


	public class XorLicenseCodec : IEncoder
	{
		private byte[] xorKey = new byte[] { 20, 177, 126, 47, 49 };

		public byte[] Decode(byte[] data)
		{
			return this.Encode(data);
		}

		public byte[] Encode(byte[] data)
		{
			byte[] buffer = new byte[data.Length];
			for (int i = 0; i < data.Length; i++)
			{
				buffer[i] = (byte)(data[i] ^ this.xorKey[i % this.xorKey.Length]);
			}
			return buffer;
		}
	}

	public class RSALicenseCodec : IEncoder
	{
		private const string hname = "MD5";
		private const int KeyLen = 1024;
		private const int MaxRSABlockSize = 86;
		private RSAParameters rsaParameters;

		public RSALicenseCodec(RSAParameters rsaParameters)
		{
			this.rsaParameters = rsaParameters;
		}

		public byte[] Encode(byte[] data)
		{
			byte[] buffer = new byte[data.Length + 128];
			using (MemoryStream stream = new MemoryStream(data))
			{
				int num;
				MemoryStream stream2 = new MemoryStream(buffer);
				RSACryptoServiceProvider key = new RSACryptoServiceProvider(1024);
				byte[] buffer2 = new byte[86];
				byte[] outputBuffer = new byte[86];
				HashAlgorithm algorithm = new MD5CryptoServiceProvider();
				algorithm.Initialize();
				while ((num = stream.Read(buffer2, 0, 86)) == 86)
				{
					algorithm.TransformBlock(buffer2, 0, 86, outputBuffer, 0);
					stream2.Write(buffer2, 0, buffer2.Length);
				}
				buffer2 = algorithm.TransformFinalBlock(buffer2, 0, num);
				stream2.Write(buffer2, 0, buffer2.Length);
				RSAParameters parameters = new RSAParameters();
				parameters.D = (byte[])this.rsaParameters.D.Clone();
				parameters.DP = (byte[])this.rsaParameters.DP.Clone();
				parameters.DQ = (byte[])this.rsaParameters.DQ.Clone();
				parameters.Exponent = (byte[])this.rsaParameters.Exponent.Clone();
				parameters.InverseQ = (byte[])this.rsaParameters.InverseQ.Clone();
				parameters.Modulus = (byte[])this.rsaParameters.Modulus.Clone();
				parameters.P = (byte[])this.rsaParameters.P.Clone();
				parameters.Q = (byte[])this.rsaParameters.Q.Clone();
				key.ImportParameters(parameters);
				AsymmetricSignatureFormatter formatter = new RSAPKCS1SignatureFormatter(key);
				formatter.SetHashAlgorithm("MD5");
				outputBuffer = formatter.CreateSignature(algorithm.Hash);
				stream2.Write(outputBuffer, 0, outputBuffer.Length);
				stream2.Close();
				stream.Close();
			}
			return buffer;
		}
	}

	public interface IEncoder
	{
		byte[] Encode(byte[] data);
	}

	public class EncoderSequence : IEncoder
	{
		private IEncoder[] encoders;

		public EncoderSequence(params IEncoder[] encoders)
		{
			this.encoders = encoders;
		}

		public byte[] Encode(byte[] data)
		{
			byte[] buffer = data;
			for (int i = 0; i < this.encoders.Length; i++)
			{
				buffer = this.encoders[i].Encode(buffer);
			}
			return buffer;
		}
	}

	public class Base32Decoder
	{
		private static byte CharToVal(char c)
		{
			c = char.ToLower(c);
			if (('0' <= c) && (c <= '9'))
			{
				return (byte)TrimNegative((byte)(c - '0'));
			}
			return (byte)TrimNegative((byte)((c - 'a') + 10));
		}


		private static byte[] ConvertFromKey(string key, byte base2)
		{
			if (IsEmpty(key))
			{
				return null;
			}
			uint num = (uint)Math.Floor((double)((((double)base2) / 8.0) * key.Length));
			byte[] buffer = new byte[num];
			if (IsEmpty(buffer))
			{
				return null;
			}
			for (int i = 0; i < buffer.Length; i++)
			{
				buffer[i] = 0;
			}
			int num3 = 0;
			int num4 = key.Length - 1;
			byte num5 = 0;
			while ((num4 >= 0) && (num3 < (num * 8)))
			{
				byte num6 = CharToVal(key[num4--]);
				int num7 = (buffer[num3 / 8] + (num6 << (num3 % 8))) + num5;
				num5 = (byte)(num7 / 0x100);
				buffer[num3 / 8] = (byte)(num7 % 0x100);
				num3 += base2;
			}
			return buffer;
		}


		public static byte[] Decode(string str)
		{
			return ConvertFromKey(str, 5);
		}


		private static bool IsEmpty(Array value)
		{
			if (value != null)
			{
				return (value.Length == 0);
			}
			return true;
		}

		private static bool IsEmpty(string value)
		{
			if (value != null)
			{
				return (value.Length == 0);
			}
			return true;
		}


		private static int TrimNegative(int x)
		{
			if (x < 0)
			{
				return 0;
			}
			return x;
		}

	}

	public class Base32Encoder
	{
		private static uint Bits16(uint number16, int from, int to)
		{
			return ((number16 >> to) & (((uint)Math.Pow(2.0, (double)(from - to))) - 1));
		}

		private static string ConvertToNumber(byte[] longNumber, byte base2)
		{
			string str = string.Empty;
			for (int i = 0; i < (longNumber.Length * 8); i += base2)
			{
				uint num2 = longNumber[i / 8];
				if (((i % 8) > (8 - base2)) && ((i / 8) < (longNumber.Length - 1)))
				{
					num2 += (uint)(longNumber[(i / 8) + 1] << 8);
				}
				byte b = (byte)Bits16(num2, (i % 8) + base2, i % 8);
				str = ValToDigit(b) + str;
			}
			return str;
		}

		public static string Encode(byte[] data)
		{
			return ConvertToNumber(data, 5);
		}

		private static char ValToDigit(byte b)
		{
			if (b <= 9)
			{
				return (char)(b + 0x30);
			}
			return (char)((b - 10) + 0x61);
		}
	}

	




	public class Deprotector
	{
		private IEncoder encoder;

		public Deprotector(IEncoder encoder)
		{
			this.encoder = encoder;
		}

		public string GenerateLicense(License license)
		{
			return string.Format("N{0}", new NewLicenseSerializer().Serialize(license, encoder));
		}
	}

	public class NewLicenseSerializer
	{
		public string Serialize(License license, IEncoder encoder)
		{
			byte[] buffer;
			using (MemoryStream stream = new MemoryStream())
			{
				WriteLicenseToStream(license, stream);
				buffer = stream.ToArray();
			}
			return Base32Encoder.Encode(encoder.Encode(buffer));
		}

		private static void WriteBytePrefixedString(BinaryWriter writer, string str)
		{
			writer.Write((byte)str.Length);
			writer.Write(str.ToCharArray());
		}

		private static void WriteDateTime(BinaryWriter writer, DateTime date)
		{
			writer.Write(date.Ticks);
		}

		private static void WriteLicenseToStream(License license, Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8);
			writer.Write(license.Version);
			writer.Write((byte)license.Type);
			writer.Write((byte)license.Binding);
			writer.Write(license.Capacity);
			WriteBytePrefixedString(writer, license.LicensedTo);
			WriteDateTime(writer, license.StartTime);
			WriteDateTime(writer, license.EndTime);
			WriteBytePrefixedString(writer, license.LicenseId.ToString());
			WriteBytePrefixedString(writer, license.PurchaseId);
			WriteDateTime(writer, license.PurchaseDate);
		}

	}
}