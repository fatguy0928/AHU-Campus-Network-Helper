using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

public static class IconTests
{
    public static int Main(string[] args)
    {
        try
        {
            string path=args[0];
            byte[] data=File.ReadAllBytes(path);
            if(data.Length<6 || BitConverter.ToUInt16(data,0)!=0 || BitConverter.ToUInt16(data,2)!=1) throw new Exception("invalid ICO header");
            int count=BitConverter.ToUInt16(data,4);
            var sizes=new HashSet<int>();
            var offsets=new Dictionary<int,int>();
            var lengths=new Dictionary<int,int>();
            for(int i=0;i<count;i++)
            {
                int offset=6+i*16;
                if(offset+16>data.Length) throw new Exception("truncated ICO directory");
                int width=data[offset]==0?256:data[offset];
                int height=data[offset+1]==0?256:data[offset+1];
                if(width!=height) throw new Exception("icon entry is not square");
                sizes.Add(width);
                lengths[width]=(int)BitConverter.ToUInt32(data,offset+8);
                offsets[width]=(int)BitConverter.ToUInt32(data,offset+12);
            }
            foreach(int size in new[]{16,24,32,48,64,128,256}) if(!sizes.Contains(size)) throw new Exception("missing "+size+"px icon");
            foreach(int size in sizes)
            {
                int offset=offsets[size],length=lengths[size];
                if(offset<0 || length<=0 || offset+length>data.Length) throw new Exception(size+"px frame is outside the ICO file");
                using(var frame=new MemoryStream(data,offset,length,false))
                using(var bitmap=new Bitmap(frame))
                {
                    if(bitmap.Width!=size || bitmap.Height!=size) throw new Exception(size+"px frame cannot be rendered at its declared size");
                    if(size==256 && bitmap.GetPixel(0,0).A!=0) throw new Exception("icon corner must be transparent");
                }
            }
            Console.WriteLine("PASS ICO contains transparent 16, 24, 32, 48, 64, 128 and 256px images");
            return 0;
        }
        catch(Exception e) {Console.WriteLine("FAIL icon asset: "+e.Message);return 1;}
    }
}
