/*
部分内容参考了 https://github.com/LogosBible/bsdiff.net 的实现

Copyright 2010-2024 Logos Bible Software

Permission is hereby granted, free of charge, to any person obtaining a copy of
this software and associated documentation files (the "Software"), to deal in
the Software without restriction, including without limitation the rights to
use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
of the Software, and to permit persons to whom the Software is furnished to do
so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.


Copyright 2003-2005 Colin Percival
All rights reserved

Redistribution and use in source and binary forms, with or without
modification, are permitted providing that the following conditions
are met:
1. Redistributions of source code must retain the above copyright
    notice, this list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright
    notice, this list of conditions and the following disclaimer in the
    documentation and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE AUTHOR ``AS IS'' AND ANY EXPRESS OR
IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
ARE DISCLAIMED.  IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY
DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS
OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION)
HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT,
STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING
IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
POSSIBILITY OF SUCH DAMAGE.
*/

using System;
using System.IO;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.BZip2;

namespace PCL.Core.Utils.Diff;


public class BsDiff : IBinaryDiff
{
	private const int HeaderSize = 32; // 32-byte header
	private const int HeaderVersionIndex = 0;
	private const long HeaderVersion = 0x3034464649445342; // "BSDIFF40" in little-endian
	private const int HeaderCtrlIndex = 8;
	private const int HeaderDiffIndex = 16;
	private const int HeaderNewSizeIndex = 24;

	/*
File format:
	0	8	"BSDIFF40"
	8	8	X
	16	8	Y
	24	8	sizeof(newfile)
	32	X	bzip2(control block)
	32+X	Y	bzip2(diff block)
	32+X+Y	???	bzip2(extra block)
with control block a set of triples (x,y,z) meaning "add x bytes
from oldfile to x bytes from the diff block; copy y bytes from the
extra block; seek forwards in oldfile by z bytes".
*/
	
	public async Task<byte[]> ApplyAsync(byte[] originData, byte[] diffData)
	{
		return await Task.Run(() =>
		{
			if (diffData.Length < HeaderSize)
				throw new Exception("Diff file size is less than the header size");
			if (BitConverter.ToInt64(diffData, HeaderVersionIndex) != HeaderVersion)
				throw new Exception("Diff file version is wrong");
			// 读取 Header 信息
			var ctrlLen = BitConverter.ToInt64(diffData, HeaderCtrlIndex);
			var diffLen = BitConverter.ToInt64(diffData, HeaderDiffIndex);
			var newLen = BitConverter.ToInt64(diffData, HeaderNewSizeIndex);
			var extraLen = diffData.Length - HeaderSize - ctrlLen - diffLen;

			if (ctrlLen < 0 || diffLen < 0 || extraLen < 0)
				throw new Exception("Block size is negative");
			if (newLen < 0)
				throw new Exception("Final file size info is negative");
			if (HeaderSize + ctrlLen + diffLen + extraLen > diffData.Length)
				throw new Exception("Diff file size info is not correct");

			Console.WriteLine(
				$"Got diff-data-len = {diffData.Length}, ctrllen = {ctrlLen}, difflen = {diffLen}, extralen = {extraLen}, totallen = {newLen}");

			var ctrlContent = new byte[ctrlLen];
			// 获取 Control 数据
			long curOffset = HeaderSize;
			Array.Copy(diffData, curOffset, ctrlContent, 0, ctrlLen);
			using var ctrlStream = new BZip2InputStream(new MemoryStream(ctrlContent));
			using var ctrlReader = new BinaryReader(ctrlStream);
			// 获取 Diff 数据
			curOffset += ctrlLen;
			var diffContent = new byte[diffLen];
			Array.Copy(diffData, curOffset, diffContent, 0, diffLen);
			using var diffStream = new BZip2InputStream(new MemoryStream(diffContent));
			using var diffReader = new BinaryReader(diffStream);
			// 获取 Extra 数据
			curOffset += diffLen;
			var extraContent = new byte[extraLen];
			Array.Copy(diffData, curOffset, extraContent, 0, extraLen);
			using var extraStream = new BZip2InputStream(new MemoryStream(extraContent));
			using var extraReader = new BinaryReader(extraStream);
			
			var ret = new byte[newLen];

			long newDataPos = 0;
			long oldDataPos = 0;
			while (newDataPos < newLen)
			{
				var addRange = ReadInt64(ctrlReader.ReadBytes(8));
				var copyRange = ReadInt64(ctrlReader.ReadBytes(8));
				var seekPos = ReadInt64(ctrlReader.ReadBytes(8));

				Console.WriteLine($"Round add-range = {addRange}, copy-range = {copyRange}, seek-pos = {seekPos}");
				
				// 新加入的
				if (newDataPos + addRange > newLen)
					throw new Exception(
						$"Add range overflows, want add {addRange.ToString()}, but only have {newLen - newDataPos} left");

				for (long i = 0; i < addRange; i++)
				{
					var readedByte = diffReader.ReadByte();
					if (oldDataPos + i < originData.Length)
						ret[newDataPos + i] = (byte)(readedByte + originData[oldDataPos + i]);
					else
						ret[newDataPos + i] = readedByte;
				}

				newDataPos += addRange;
				oldDataPos += addRange;

				// 原有的
				if (newDataPos + copyRange > newLen)
					throw new Exception(
						$"Copy range overflows, want  copy {copyRange.ToString()}, but only have {newLen - newDataPos} left");

				for (var i = 0; i < copyRange; i++)
				{
					ret[newDataPos + i] = extraReader.ReadByte();
				}

				newDataPos += copyRange;

				// 原有的切换到指定位置继续读取
				oldDataPos += seekPos;
				if (oldDataPos > originData.Length)
					throw new Exception(
						$"Old data pos overflows, current old data length = {originData.Length}, but want {oldDataPos}");
			}

			return ret;
		});
	}

	public Task<byte[]> MakeAsync(byte[] originData, byte[] newData)
	{
		throw new NotImplementedException();
	}

	internal static long ReadInt64(byte[] buffer, int offset = 0)
	{
		// 手动组合小端序的 long 值
		var value = ((long)buffer[offset] << 0)  | ((long)buffer[offset + 1] << 8) |
		            ((long)buffer[offset + 2] << 16) | ((long)buffer[offset + 3] << 24) |
		            ((long)buffer[offset + 4] << 32) | ((long)buffer[offset + 5] << 40) |
		            ((long)buffer[offset + 6] << 48) | ((long)buffer[offset + 7] << 56);

		// 原始位运算逻辑保持不变
		var mask = value >> 63;
		return (~mask & value) |
		       (((value & unchecked((long)0x8000000000000000)) - value) & mask);
	}
}
