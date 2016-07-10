using System;
using System.IO;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using Utils;

namespace ctr_PatcherConsole
{
    class Patch
    {
        private Stream RomStream;
        private Stream PatchStream;
        public string PatchFileName;
        public string RomFileName;
        public PatchHeader Header;

        public Patch(string patchFilePath, string romFilePath)
        {
            RomStream = File.Open(romFilePath, FileMode.Open);
            PatchStream = File.OpenRead(patchFilePath);
            Header = StructReader.ReadStruct<PatchHeader>(PatchStream);
            PatchFileName = patchFilePath;
            RomFileName = romFilePath;
        }
        public Patch(byte[] patchFileData, string romFilePath)
        {
            RomStream = File.Open(romFilePath, FileMode.Open);
            PatchStream = new MemoryStream(patchFileData);
            Header = StructReader.ReadStruct<PatchHeader>(PatchStream);
            PatchFileName = "";
            RomFileName = romFilePath;
        }
        public Patch() { }

        public bool CompareByteArray(byte[] Array1, byte[] Array2)
        {
            for (int i = 0; i < Array1.Length; i++)
            {
                if (Array1[i] != Array2[i])
                    return false;
            }
            return true;
        }
        public void ApplyPatch()
        {
            bool isPatched = false;
            byte patchCommand = 0;
            BinaryReader patchReader = new BinaryReader(PatchStream);
            BinaryReader romReader = new BinaryReader(RomStream);

            if (Header.Signature != patchFileSignature)
            {
                RomStream.Close();
                PatchStream.Close();
                throw new Exception(string.Format("Patch File {0} Corrupted", PatchFileName));
            }
            if (!Header.Version.Equals(curVersion))
            {
                RomStream.Close();
                PatchStream.Close();
                throw new Exception(string.Format("Doesn't support patch version：{0}.{1}.{2}",
                    Header.Version.Major, Header.Version.Minor, Header.Version.Level));
            }

            patchCommand = patchReader.ReadByte();
            if (patchCommand == (byte)PatchCommands.Check)
            {
                isPatched = true;
            }
            Console.WriteLine("Checking files...");
            while (patchCommand == (byte)PatchCommands.Check)
            {
                ulong chkOffset = 0;
                ulong chkSize = 0;
                byte[] sha256New, sha256Old, chkData;

                chkOffset = patchReader.ReadUInt64();
                chkSize = patchReader.ReadUInt64();
                sha256New = patchReader.ReadBytes(32);
                RomStream.Position = (long)chkOffset;
                chkData = romReader.ReadBytes((int)chkSize);

                SHA256 calculator = SHA256.Create();
                sha256Old = calculator.ComputeHash(chkData);
                if (!CompareByteArray(sha256New, sha256Old))
                {
                    isPatched = false;
                    break;
                }
                patchCommand = patchReader.ReadByte();
            }
            if (isPatched)
            {
                RomStream.Close();
                PatchStream.Close();
                throw new Exception(string.Format("File：{0} has been updated to latest version", RomFileName));
            }
            Console.WriteLine("Check OK!");

            PatchStream.Seek(Marshal.SizeOf(typeof(PatchHeader)), SeekOrigin.Begin);
            patchCommand = patchReader.ReadByte();

            ConsoleProcessBar processBar = new ConsoleProcessBar();
            processBar.Minimum = Marshal.SizeOf(typeof(PatchHeader));
            processBar.Maximum = PatchStream.Length - (PatchStream.Length - (long)Header.ExtDataOffset);
            long lastPercent = 0;

            Console.WriteLine("Applying patch...");
            Console.Write("{0}  {1}%", processBar.Bar, processBar.Percent);
            while (true)
            {
                if (patchCommand == (byte)PatchCommands.Over)
                {
                    break;
                }
                else if (patchCommand == (byte)PatchCommands.Check)
                {
                    PatchStream.Seek(48, SeekOrigin.Current);
                }
                else if (patchCommand == (byte)PatchCommands.Move)
                {
                    long srcOffset, dstOffset, size;
                    srcOffset = patchReader.ReadInt64();
                    dstOffset = patchReader.ReadInt64();
                    size = patchReader.ReadInt64();
                    MoveData(srcOffset, dstOffset, size);
                }
                else if (patchCommand == (byte)PatchCommands.Set)
                {
                    long startOffset, length;
                    byte data = 0;
                    startOffset = (long)patchReader.ReadUInt64();
                    length = (long)patchReader.ReadUInt64();
                    data = patchReader.ReadByte();
                    SetData(startOffset, length, data);
                }
                else if (patchCommand == (byte)PatchCommands.ChangeSize)
                {
                    RomStream.SetLength((long)patchReader.ReadUInt64());
                }
                else if (patchCommand >= (byte)PatchCommands.SeekWrite && patchCommand <= (byte)PatchCommands.SeekWrite + 0xF)
                {
                    bool isSeekSet = (patchCommand & 8) == 0;
                    long offset = 0;
                    uint length = 0;
                    const uint bufferSize = 0x10000;
                    byte[] buffer = new byte[bufferSize];
                    uint offsetByteLength = (uint)(1 << (patchCommand >> 1 & 3));
                    uint sizeByteLength = (uint)(1 << (patchCommand & 1));
                    byte[] offsetBytes = new byte[8];
                    byte[] sizeBytes = new byte[4];
                    PatchStream.Read(offsetBytes, 0, (int)offsetByteLength);
                    PatchStream.Read(sizeBytes, 0, (int)sizeByteLength);
                    offset = BitConverter.ToInt64(offsetBytes, 0);
                    length = BitConverter.ToUInt32(sizeBytes, 0);
                    length++;
                    buffer = patchReader.ReadBytes((int)length);
                    SeekWrite(isSeekSet, offset, length, buffer);
                }
                else
                    throw new Exception("Patch File Corrupted");

                patchCommand = patchReader.ReadByte();

                processBar.Value = PatchStream.Position;
                if (processBar.Percent > lastPercent)
                {
                    Console.CursorLeft = 0;
                    Console.Write("{0}  {1}%", processBar.Bar, processBar.Percent);
                }
                lastPercent = processBar.Percent;
            }
            RomStream.Close();
            PatchStream.Close();
        }

        private void MoveData(long srcOffset, long dstOffset, long length)
        {
            if (srcOffset != dstOffset)
            {
                const long bufferSize = 0x100000;
                byte[] buffer = new byte[bufferSize];
                int index = 0;

                if (srcOffset > dstOffset)
                {
                    while (length > 0)
                    {
                        long size = length > bufferSize ? bufferSize : length;
                        RomStream.Position = srcOffset + index * bufferSize;
                        RomStream.Read(buffer, 0, (int)size);
                        RomStream.Position = dstOffset + index * bufferSize;
                        RomStream.Write(buffer, 0, (int)size);
                        length -= size;
                        index++;
                    }
                }
                else
                {
                    while (length > 0)
                    {
                        long size = length > bufferSize ? bufferSize : length;
                        length -= size;
                        RomStream.Position = srcOffset + length;
                        RomStream.Read(buffer, 0, (int)size);
                        RomStream.Position = dstOffset + length;
                        RomStream.Write(buffer, 0, (int)size);
                    }
                }
            }

        }
        private void SetData(long startOffset, long length, byte data)
        {
            RomStream.Position = startOffset;
            while (length > 0)
            {
                RomStream.WriteByte(data);
                length--;
            }
        }
        private void SeekWrite(bool isSeekSet, long offset, uint length, byte[] data)
        {
            RomStream.Seek(offset, isSeekSet ? SeekOrigin.Begin : SeekOrigin.Current);
            RomStream.Write(data, 0, (int)length);
        }

        const uint patchFileSignature = 0x00535033;
        readonly Version curVersion = new Version { Major = 1, Minor = 0, Level = 0 };
        public enum PatchCommands : byte
        {
            Over,
            Check,
            Move,
            Set,
            ChangeSize,
            SeekWrite = 0x10
        };
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Version
        {
            public byte Major;
            public byte Minor;
            public byte Level;

            public override bool Equals(object obj)
            {
                if (obj == null)
                {
                    return false;
                }
                if (obj is Version)
                {
                    var a = (Version)obj;
                    return (a.Level == Level) && (a.Major == Major) && (a.Minor == Minor);
                }
                return base.Equals(obj);
            }
            public override int GetHashCode()//avoid warning. 
            {
                return base.GetHashCode();
            }
        }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct PatchHeader
        {
            public uint Signature;
            public Version Version;
            public byte Reversed;
            public ulong ExtDataOffset;
        }
    }
}
