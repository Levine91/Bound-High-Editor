using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

// Proto 2 keeps its own code, data, cartridge header and vectors.
static class Proto2Patch {
    const int Base=0x07000000+EnemyPatch.PatchStart;
    internal static void Save(V810 a){a.I(41,3,3,-144);a.I(55,3,1,16);a.I(55,3,2,20);for(int r=4;r<32;r++)a.I(55,3,r,24+(r-4)*4);}
    internal static void Restore(V810 a,int except=0){a.I(51,3,1,16);a.I(51,3,2,20);for(int r=4;r<32;r++)if(r!=except)a.I(51,3,r,24+(r-4)*4);a.I(41,3,3,144);}
    internal static void Hook(byte[] rom,byte[] original,int offset,int target,bool link){if(!rom.Skip(offset).Take(4).SequenceEqual(original.Skip(offset).Take(4)))throw new InvalidDataException("Proto 2 hook conflict.");var a=new V810(0x07000000+offset);a.J(target,link);Array.Copy(a.Finish(),0,rom,offset,4);}
    static void Branch(V810 a,int condition,string label){string skip="near"+a.Address;a.B(condition^8,skip);a.J(label);a.Label(skip);}
    static void Stage(V810 a,int r){a.Const(r,0x05000004);a.I(49,r,r);}
    static void Get(V810 a,int table,int result,string missing){Stage(a,result);a.Const(11,91);a.I(3,11,result);Branch(a,9,missing);a.I(20,2,result);a.Const(11,Base+table);a.I(1,11,result);a.I(51,result,result);}
    static void Store(V810 a,int address,int r){a.Const(11,address);a.I(53,11,r);}
    static void Timer(V810 a,int table,int target){string done="timer"+a.Address;Get(a,table,10,done);a.I(19,-1,10);Branch(a,2,done);Store(a,target,10);a.Label(done);}
    internal static byte[] Apply(Document doc,byte[] rom,List<byte> payload){
        var a=new V810(Base+payload.Count);var hooks=new List<int[]>();
        int display=a.Address;Save(a);a.J(0x07020bb4,true);
        a.Const(10,0x0500000a);a.I(49,10,10);a.I(19,0,10);Branch(a,10,"displayDone");
        Get(a,0,10,"displayDone");a.I(19,0,10);Branch(a,2,"displayDone");
        a.Label("block");a.I(51,10,11);a.I(19,0,11);Branch(a,2,"redraw");a.I(51,10,12,4);a.I(51,10,14,8);a.I(17,12,10);
        a.Const(15,0x07000000);a.I(3,15,11);Branch(a,9,"block"); // ROM pointer overrides are consumed by descriptor hooks.
        a.Label("copy");a.I(49,14,13);a.I(53,11,13);a.I(17,2,14);a.I(17,2,11);a.I(17,-1,12);Branch(a,10,"copy");a.J("block");
        a.Label("redraw");a.J(0x07020888,true);a.Label("displayDone");Restore(a);a.I(6,31,0);hooks.Add(new[]{0x19042,display,1});
        int behavior=a.Address;Save(a);Stage(a,6);a.Const(11,91);a.I(3,11,6);Branch(a,9,"aiDone");a.I(20,3,6);a.I(1,23,6);a.Const(11,Base+400);a.I(1,11,6);a.I(48,6,6);a.Label("aiDone");Restore(a,6);a.I(6,31,0);hooks.Add(new[]{0xeba8,behavior,1});
        int init=a.Address;Save(a);Get(a,1200,10,"nativeInit");a.I(19,0,10);Branch(a,2,"nativeInit");Restore(a);a.J(0x07003206,false);a.Label("nativeInit");Restore(a);a.I(47,0,1,0x500);a.J(0x070030e4,false);hooks.Add(new[]{0x30e0,init,0});
        int wind=a.Address;Save(a);a.J(0x07009a56,true);a.Const(20,0x05001ce8);a.I(49,20,20);
        Get(a,23600,10,"windSetting");a.I(19,-1,10);Branch(a,2,"windSetting");Store(a,0x05001cee,10);
        a.Label("windSetting");Get(a,1600,10,"windTimer");a.I(19,-1,10);Branch(a,2,"windTimer");Store(a,0x05001ce8,10);Store(a,0x05001cea,0);Store(a,0x05001cf0,0);Store(a,0x05001cf2,0);
        a.I(19,0,10);Branch(a,2,"windTimer");a.Const(11,0x05001cee);a.I(49,11,10);a.I(19,0,10);Branch(a,10,"windCountdown");a.I(41,0,10,28);a.I(53,11,10);
        a.Label("windCountdown");a.I(19,0,20);Branch(a,10,"windTimer");a.J(0x070002a6,true);a.I(21,10,10);a.Const(11,100);a.I(11,11,10);a.I(41,30,10,75);a.Const(11,0x05000006);a.I(49,11,11);a.I(20,4,11);a.Const(12,48);a.I(2,11,12);a.I(8,12,10);a.I(21,5,10);Store(a,0x05001cec,10);
        a.Label("windTimer");Timer(a,2800,0x05001cec);Restore(a);a.I(6,31,0);hooks.Add(new[]{0x32a4,wind,1});
        int gobbler=a.Address;Save(a);a.J(0x0700b896,true);Get(a,2400,10,"gobblerTimer");a.I(19,-1,10);Branch(a,2,"gobblerTimer");Store(a,0x05001d2c,10);a.I(19,0,10);Branch(a,2,"gobblerTimer");
        a.Const(11,0x050020e8);a.I(49,11,10);a.I(19,0,10);Branch(a,15,"gobblerTimer");a.I(41,0,10,500);a.I(53,11,10);a.Const(12,0x05000006);a.I(49,12,12);a.I(16,3,13);a.I(2,12,13);a.I(8,13,10);a.I(23,1,10);Store(a,0x050020e6,10);
        a.Label("gobblerTimer");Timer(a,3600,0x050020e6);Restore(a);a.I(6,31,0);hooks.Add(new[]{0x32a8,gobbler,1});
        int hidden=a.Address;Save(a);a.J(0x0700938c,true);a.Const(10,0x05000022);a.I(49,10,10);a.I(19,0,10);Branch(a,10,"hiddenDone");a.Const(10,0x0500000a);a.I(49,10,10);a.I(19,0,10);Branch(a,10,"hiddenDone");
        Stage(a,10);a.Const(11,91);a.I(3,11,10);Branch(a,9,"hiddenDone");a.I(0,10,11);a.I(20,7,10);a.I(20,5,11);a.I(1,11,10);a.Const(11,Base+7600);a.I(1,11,10);a.Const(11,0x05000fd8);a.Const(12,160);
        a.Label("hiddenLoop");a.I(48,10,13);a.I(19,-1,13);Branch(a,2,"hiddenNext");a.I(53,11,13);a.Label("hiddenNext");a.I(17,1,10);a.I(17,2,11);a.I(17,-1,12);Branch(a,10,"hiddenLoop");a.Label("hiddenDone");Restore(a);a.I(6,31,0);hooks.Add(new[]{0x32ac,hidden,1});
        int music=a.Address;Save(a);Get(a,2000,10,"nativeMusic");a.I(19,-1,10);Branch(a,2,"nativeMusic");a.I(0,10,6);a.Const(11,25);a.I(3,11,6);Branch(a,9,"effect");a.J(0x0703d87a,true);a.J("musicDone");
        a.Label("effect");a.I(41,6,20,-25);a.I(16,0,6);a.J(0x0703d87a,true);a.I(0,20,6);a.I(16,15,7);a.I(16,15,8);a.J(0x0703d7f8,true);a.J("musicDone");
        a.Label("nativeMusic");Restore(a);a.I(41,3,3,-36);a.J(0x07001fd6,false);a.Label("musicDone");Restore(a);a.I(6,31,0);hooks.Add(new[]{0x1fd2,music,0});
        int update=a.Address;Save(a);Get(a,1200,10,"nativeUpdate");a.I(19,0,10);Branch(a,2,"plasmaUpdate");Restore(a);a.J(0x070086fa,false);
        a.Label("plasmaUpdate");a.Const(10,0x05000022);a.I(49,10,10);a.I(19,2,10);Branch(a,10,"nativeUpdate");Get(a,1600,10,"plasmaGobbler");a.I(19,1,10);Branch(a,10,"plasmaGobbler");a.J(0x0701154e,true);
        a.Label("plasmaGobbler");Get(a,2400,10,"nativeUpdate");a.I(19,1,10);Branch(a,10,"nativeUpdate");a.J(0x0700b9dc,true);
        a.Label("nativeUpdate");Restore(a);a.I(47,0,1,0x500);a.J(0x070085a8,false);hooks.Add(new[]{0x85a4,update,0});
        foreach(var h in new[]{new[]{0x116a4,3200,0x05001cec,11,1,0x1cec},new[]{0x116d8,3200,0x05001cec,11,1,0x1cec},new[]{0xbb0a,4000,0x050020e6,11,4,-0x5f1a}}){int entry=a.Address;a.I(53,h[4],h[3],h[5]);Save(a);Timer(a,h[1],h[2]);Restore(a);a.I(6,31,0);hooks.Add(new[]{h[0],entry,1});}
        int electricDispatch=a.Address;Save(a);a.I(51,3,1,16);a.I(49,1,14,0x12b6);Electric(a,23,16,"dispatchNative");
        a.I(19,1,14);Branch(a,3,"dispatchKind");a.I(19,10,14);Branch(a,2,"dispatchKind");a.I(19,12,14);Branch(a,10,"dispatchNative");
        a.Label("dispatchKind");a.I(19,1,10);Branch(a,10,"dispatchOff");a.I(16,1,14);a.J("dispatchNative");a.Label("dispatchOff");a.I(19,1,14);Branch(a,10,"dispatchNative");a.I(16,0,14);
        a.Label("dispatchNative");a.I(55,3,14,64);Restore(a);a.I(6,31,0);hooks.Add(new[]{0x57ec,electricDispatch,1});
        foreach(var h in new[]{new[]{0x587a,14,16},new[]{0x59b4,14,16},new[]{0x5ab4,10,16},new[]{0x5b12,14,16},new[]{0x5be6,14,16},new[]{0x5fb4,16,16},new[]{0x630c,12,0}}){
            int entry=a.Address;Save(a);a.Const(10,0x05000008);a.I(49,10,10);a.I(55,3,10,24+(h[1]-4)*4);Electric(a,23,h[2],"ed"+entry);a.I(55,3,10,24+(h[1]-4)*4);a.Label("ed"+entry);Restore(a);a.I(6,31,0);hooks.Add(new[]{h[0],entry,1});
        }
        // Each original descriptor load remains the fallback. Resolve only this stage's ROM pointer overrides.
        foreach(var h in TerrainLoads(doc.Rom)){
            int entry=a.Address;int result=h[2];Save(a);a.I(41,1,9,h[1]);a.I(51,9,10);a.I(55,3,10,24+(result-4)*4);
            a.Const(10,0x0500000a);a.I(49,10,10);a.I(19,0,10);Branch(a,10,"td"+entry);Get(a,0,10,"td"+entry);a.I(19,0,10);Branch(a,2,"td"+entry);
            a.Label("ts"+entry);a.I(51,10,11);a.I(19,0,11);Branch(a,2,"td"+entry);a.I(3,9,11);Branch(a,2,"tf"+entry);a.I(17,12,10);a.J("ts"+entry);
            a.Label("tf"+entry);a.I(51,10,10,8);a.I(51,10,10);a.I(55,3,10,24+(result-4)*4);
            // Save the result over its saved register before restoring all scratch registers.
            a.Label("td"+entry);Restore(a);a.I(6,31,0);hooks.Add(new[]{h[0],entry,1});
        }
        payload.AddRange(a.Finish());int end=doc.Data.StageHud?StageHudPatch.Start(doc.Data.BossRush):doc.Data.BossRush?BossRushPatch.Start:EnemyPatch.PatchEnd;
        if(payload.Count>end-EnemyPatch.PatchStart)throw new InvalidDataException("These edits exceed the original-size ROM's free space.");payload.CopyTo(rom,EnemyPatch.PatchStart);
        foreach(var h in hooks)Hook(rom,doc.Rom,h[0],h[1],h[2]!=0);return rom;
    }
    static void Electric(V810 a,int slot,int offset,string missing){Stage(a,10);a.Const(11,91);a.I(3,11,10);Branch(a,9,missing);a.I(20,5,10);a.I(1,slot,10);a.Const(11,Base+4400+offset);a.I(1,11,10);a.I(48,10,10);a.I(19,-1,10);Branch(a,2,missing);}
    static IEnumerable<int[]> TerrainLoads(byte[] rom){
        for(int p=0;p<0x3d936;){int w=BitConverter.ToUInt16(rom,p),op=w>>10;int length=op<40?2:4;
            if(op==51 && (w&31)==1){int imm=BitConverter.ToInt16(rom,p+2),result=(w>>5)&31;if(result>=4 && (imm==0x74a4 || imm==-0x7f74 || imm==-0x3d54))yield return new[]{p,imm,result};}
            p+=length;
        }
    }
}
