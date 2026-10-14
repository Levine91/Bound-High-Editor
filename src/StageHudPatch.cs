using System;
using System.IO;
using System.Linq;

static class StageHudPatch {
    internal const int Reserve=1024;
    internal static int UsedBytes;
    internal static int Start(bool bossRush){return (bossRush?BossRushPatch.Start:EnemyPatch.PatchEnd)-Reserve;}
    internal static byte[] Apply(Document doc,byte[] input) {
        if(!doc.Data.StageHud)return input;
        int start=Start(doc.Data.BossRush);
        if(input.Length!=2097152 || !Proto2.Empty(input,start,start+Reserve,doc.IsProto2))throw new InvalidDataException("Stage HUD patch space is occupied.");
        byte[] rom=(byte[])input.Clone();var a=new V810(0x07000000+start);
        a.I(41,3,3,-128);for(int r=4;r<32;r++)a.I(55,3,r,16+(r-4)*4);
        a.J(doc.IsProto2?0x0702117c:0x0704b2e8,true); // Original HUD clears stale digits and draws all other fields.
        a.Const(12,0x05000000);a.I(48,12,10,doc.IsProto2?0x898:0x1981);a.I(19,0,10);a.B(10,"done");
        if(doc.Data.BossRush){a.I(49,12,10,doc.IsProto2?0x8ce:0x6566);a.I(19,0,10);a.B(10,"done");}
        a.I(49,12,10,doc.IsProto2?4:0x6558);a.I(41,0,11,49);a.I(3,11,10);a.B(11,"done");
        a.I(41,0,11,36);a.I(3,11,10);a.B(9,"world4");
        a.I(16,12,11);a.I(9,11,10);a.I(0,30,14);a.I(17,1,10);a.J("number");
        a.Label("world4");a.I(41,10,14,-36);a.I(16,4,10);
        a.Label("number");a.I(17,1,14);
        if(doc.IsProto2){a.Const(11,0x0715c1b0);a.I(51,11,11);}else a.I(51,12,11,0x6b4);a.Const(13,0x00025010);
        for(int i=0;i<5;i++){a.I(49,11,15,i*2);a.I(53,13,15,i*2);}
        if(doc.IsProto2)a.Const(12,0x07158ef8);else a.I(41,12,12,0x968);Digit(a,10,12,13,10);
        // Native fixed character439 is an eight-pixel, two-row bar. Palette2
        // maps it to the same bright red as the untouched native digit font.
        a.I(41,0,15,0x81b7);a.I(53,13,15,0x8c);
        a.I(16,10,11);a.I(3,11,14);a.B(1,"oneDigit");
        a.I(0,14,10);a.I(9,11,10);a.I(0,30,14);Digit(a,10,12,13,14);Digit(a,14,12,13,16);a.J("done");
        a.Label("oneDigit");Digit(a,14,12,13,14);
        a.Label("done");for(int r=4;r<32;r++)a.I(51,3,r,16+(r-4)*4);a.I(41,3,3,128);a.I(6,31,0);
        byte[] payload=a.Finish();UsedBytes=payload.Length;
        if(payload.Length>Reserve)throw new InvalidDataException("Stage HUD patch exceeds its reserved space.");
        Array.Copy(payload,0,rom,start,payload.Length);
        int hook=doc.IsProto2?0x19052:0x5a96a;
        if(!rom.Skip(hook).Take(4).SequenceEqual(doc.Rom.Skip(hook).Take(4)))throw new InvalidDataException("Stage HUD hook conflict.");
        var jump=new V810(0x07000000+hook);jump.J(0x07000000+start,true);Array.Copy(jump.Finish(),0,rom,hook,4);
        return rom;
    }
    static void Digit(V810 a,int value,int table,int target,int offset){
        a.I(0,value,15);a.I(20,2,15);a.I(1,table,15);a.I(51,15,15);
        a.I(49,15,16);a.I(53,target,16,offset);a.I(49,15,16,2);a.I(53,target,16,offset+128);
    }
}
