using System;
using System.IO;
using System.Linq;

static class BundledPatch {
    internal static byte[] Apply(Document doc,byte[] input,string resource) {
        if(!doc.IsProto2)throw new InvalidDataException("This enhancement requires the original Proto 2 ROM.");
        byte[] rom=(byte[])input.Clone();
        using(var stream=typeof(Document).Assembly.GetManifestResourceStream(resource))using(var r=new BinaryReader(stream)) {
            if(new string(r.ReadChars(5))!="PATCH")throw new InvalidDataException("Invalid bundled enhancement patch.");
            while(true) {
                int at=(r.ReadByte()<<16)|(r.ReadByte()<<8)|r.ReadByte();
                if(at==0x454f46){if(stream.Position!=stream.Length)throw new InvalidDataException("Unexpected enhancement patch trailer.");break;}
                int count=(r.ReadByte()<<8)|r.ReadByte();byte[] bytes;
                if(count==0){count=(r.ReadByte()<<8)|r.ReadByte();byte value=r.ReadByte();bytes=Enumerable.Repeat(value,count).ToArray();}
                else bytes=r.ReadBytes(count);
                if(count==0 || bytes.Length!=count || at>rom.Length-count)throw new InvalidDataException("Invalid enhancement patch range.");
                for(int i=0;i<count;i++)if(rom[at+i]!=doc.Rom[at+i])throw new InvalidDataException("enhancement patch conflicts with another edit.");
                Array.Copy(bytes,0,rom,at,count);
            }
        }
        return rom;
    }
}
