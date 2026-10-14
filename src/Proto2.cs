using System;
using System.IO;
using System.Linq;

static class Proto2 {
    internal const string Hash="c9d4181ed56e2520ae42a74afaeaff9902180f86";
    internal static bool Empty(byte[] rom,int start,int end,bool proto){for(int i=start;i<end;i++)if(rom[i]!=(proto && (((i>>9)^i)&16)!=0?255:0))return false;return true;}
    internal static int TerrainPointer(byte[] rom,int type){int index=BitConverter.ToUInt16(rom,0x41438+type*2);return 0x07000000+(type>=20 && type<=25?0x15808c:type>=26 && type<=34?0x15c2ac:0x1574a4)+index*4;}
    static readonly int[][] Banks={
        new[]{0x82bca,0x12a7c8},
        new[]{0x9d04c,0x11a7c8},
        new[]{0xb7560,0x1267c8},
        new[]{0xc1770,0x11e7c8},
        new[]{0xd4d32,0x1227c8},
        new[]{0xe74ca,0xea7c8},
        new[]{0xf28fa,0xfa7c8},
        new[]{0xf79fa,0xf67c8},
        new[]{0xfbbfa,0x10a7c8},
        new[]{0x10dd44,0xe67c8},
        new[]{0x113996,0x1167c8},
        new[]{0x12d538,0xf27c8},
        new[]{0x1336a0,0x1067c8},
        new[]{0x149810,0xfe7c8},
        new[]{0x1538e6,0x1127c8},
        new[]{0x17300c,0x10e7c8},
        new[]{0x180d58,0x1027c8},
        new[]{0x184d58,0x13e7c8},
        new[]{0x18ec32,0x13a7c8},
        new[]{0x192c32,0xee7c8}
    };
    internal static int Asset(int address){foreach(var b in Banks)if(address>=b[0] && address<b[0]+16384)return address-b[0]+b[1];throw new InvalidDataException("Unmapped Proto 2 graphics address.");}
}
