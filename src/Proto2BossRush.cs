using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

// 08CE is alignment padding after the five password bytes at08C8, before08D0.
static class Proto2BossRush {
    internal const int Flag=0x050008ce;
    internal static byte[] Apply(Document doc,byte[] input){
        if(!Proto2.Empty(input,BossRushPatch.Start,EnemyPatch.PatchEnd,true))throw new InvalidDataException("Boss Rush patch space is occupied.");
        byte[] rom=(byte[])input.Clone();var data=new List<byte>();int origin=0x07000000+BossRushPatch.Start;
        data.AddRange(doc.Rom.Skip(0xbf996).Take(7*46));data.AddRange(doc.Rom.Skip(0xbf996+46).Take(46));data.AddRange(doc.Rom.Skip(0xbf996+7*46).Take(46));
        int name=origin+data.Count;foreach(int g in new[]{0x7ff,0x7ff,0x7ff,0x7ff,0x7ff,0x219,0x1fb,0x1f2,0x1f2,0x7ff,0x1f7,0x1f0,0x1f2,0x233,0x7ff,0x7ff,0x7ff,0x7ff,0x7ff})data.AddRange(BitConverter.GetBytes((ushort)g));
        var a=new V810(origin+data.Count);var hooks=new List<int[]>();
        int draw=a.Address;Proto2Patch.Save(a);
        Copy(a,origin,36,23,9,-2);Copy(a,0x070bfb06,50,21,1,-2);Copy(a,0x070bf56a,51,30,1,-2);
        int[] rows={0x0705d376,0x070bf51e,0x070bf544,0x070bf5a6,name};
        for(int i=0;i<5;i++){a.I(16,0,14);a.I(19,i,21);a.B(2,"bright"+i);a.Const(14,0x8000);a.Label("bright"+i);Copy(a,rows[i],45+i,19,1,-1);}
        a.Const(10,0x05003000);a.I(41,0,11,408);a.I(53,10,11,17*36+12);a.I(41,0,11,71);a.I(53,10,11,15*36+16);a.I(41,0,11,39);a.I(53,10,11,14*36+16);
        a.I(0,21,11);a.I(20,3,11);a.I(41,11,11,160);a.I(53,10,11,13*36+6);a.I(53,10,0,16*36);a.I(53,10,0,17*36);
        Proto2Patch.Restore(a);a.I(6,31,0);
        int loop=a.Address;Proto2Patch.Save(a);a.I(48,4,10,0x760d);a.I(19,2,10);a.B(10,"noDraw");a.J(draw,true);a.Label("noDraw");a.I(48,4,10,0x760d);a.I(19,1,10);a.B(10,"noHelp");a.Const(10,0x05003264);a.I(41,0,11,unchecked((short)0xc000));a.I(53,10,11);a.Label("noHelp");Proto2Patch.Restore(a);a.I(48,4,11,0x7612);a.J(0x07023af2,false);hooks.Add(new[]{0x23aee,loop,0});
        int navigate=a.Address;a.I(0,21,23);a.J(0x07023aee,false);hooks.Add(new[]{0x2337c,navigate,0});
        int choose=a.Address;a.Const(10,Flag);a.I(53,10,0);a.I(19,4,21);a.B(10,"ordinary");a.I(16,1,11);a.I(53,10,11);a.Const(10,0x05000000);a.I(52,10,0,0x898);a.I(52,10,0,0x899);foreach(int off in new[]{0x31d4,0x31f8,0x321c})a.I(53,10,0,off);a.J(0x07023598,false);
        a.Label("ordinary");a.Const(10,0x05003240);a.I(41,0,11,unchecked((short)0xc000));a.I(53,10,11);a.I(0,21,11);a.I(19,1,11);a.J(0x070232bc,false);hooks.Add(new[]{0x232b8,choose,0});
        int begin=a.Address;a.Const(10,Flag);a.I(49,10,10);a.I(19,0,10);a.B(2,"nativeBegin");a.Const(10,0x05000000);a.I(16,10,11);a.I(52,10,11,0x89a);foreach(int off in new[]{0x89b,0x89c,0x8bb,0x93f})a.I(52,10,0,off);a.I(16,-1,11);for(int i=0;i<3;i++)a.I(52,10,11,0x8c5+i);a.J(0x07023c52,false);
        a.Label("nativeBegin");if(doc.Data.Sram)a.J(0x07023bda,false);else{a.I(47,0,1,0x500);a.J(0x07023bdc,false);}hooks.Add(new[]{doc.Data.Sram?0x23b96:0x23bd8,begin,0});
        int advance=a.Address;a.Const(11,Flag);a.I(49,11,11);a.I(19,0,11);a.B(2,"normalAdvance");int[] bosses={10,22,34,47,49};for(int i=0;i<4;i++){a.Const(11,bosses[i]);a.I(3,11,10);a.B(10,"next"+i);a.Const(11,bosses[i+1]);a.J(0x07001aa6,false);a.Label("next"+i);}a.Label("normalAdvance");a.I(41,10,11,1);a.J(0x07001aa6,false);hooks.Add(new[]{0x1aa2,advance,0});
        int endingWait=a.Address;a.Const(6,Flag);a.I(49,6,6);a.I(19,0,6);a.B(2,"normalWait");a.I(48,3,6,0xa0);a.I(19,0,6);a.B(2,"normalWait");a.I(52,3,0,0x9c);a.Label("normalWait");a.I(48,3,6,0x9c);a.J(0x0703a0a8,false);hooks.Add(new[]{0x3a0a4,endingWait,0});
        int cancel=a.Address;a.Const(10,Flag);a.I(49,10,10);a.I(19,0,10);a.B(2,"normalCancel");a.J("reset");a.Label("normalCancel");a.I(47,0,1,0x500);a.J(0x07023864,false);hooks.Add(new[]{0x23860,cancel,0});
        int entry=a.Address;a.Const(10,Flag);a.I(49,10,10);a.I(19,0,10);a.B(2,"normalEntry");a.Const(10,0x0500002c);a.I(49,10,10);a.I(19,-1,10);a.B(2,"finish");a.J("reset");
        a.Label("finish");a.J(0x070389d4,true);a.I(16,0,6);a.J(0x0703d87a,true);
        a.Label("reset");a.Const(10,0x05000000);a.I(53,10,0,0x8ce);a.I(53,10,0,0x2c);foreach(int off in new[]{0x898,0x899,0x89b,0x89c,0x940})a.I(52,10,0,off);a.I(16,-1,11);a.I(52,10,11,0x89a);
        if(doc.Data.Sram){a.Const(12,0x06000000);for(int i=0;i<3;i++){a.I(48,12,11,8+i*2);a.I(52,10,11,0x8c5+i);}a.I(19,-1,11);a.B(2,"noSavedPassword");a.I(16,1,11);a.I(52,10,11,0x899);a.Label("noSavedPassword");}
        a.Label("normalEntry");a.I(55,4,0,0x7600);a.J(0x070227b0,false);hooks.Add(new[]{0x227ac,entry,0});
        data.AddRange(a.Finish());BossRushPatch.UsedBytes=data.Count;if(data.Count>4096)throw new InvalidDataException("Proto 2 Boss Rush exceeds its reserved space.");data.CopyTo(rom,BossRushPatch.Start);foreach(var h in hooks)Proto2Patch.Hook(rom,doc.Rom,h[0],h[1],h[2]!=0);
        var limit=new V810(0x07023354);limit.I(19,4,11);Array.Copy(limit.Finish(),0,rom,0x23354,2);return rom;
    }
    static void Copy(V810 a,int source,int y,int width,int height,int palette){
        string label="copy"+a.Address;a.Const(10,source);a.Const(11,0x20000+y*128);a.Const(12,height);if(palette>=0)a.Const(14,palette<<14);
        a.Label(label+"row");a.Const(13,width);a.Label(label);a.I(49,10,15);if(palette!=-2){a.I(45,15,15,0x3fff);a.I(12,14,15);}a.I(53,11,15);a.I(17,2,10);a.I(17,2,11);a.I(17,-1,13);a.B(10,label);a.I(41,11,11,128-width*2);a.I(17,-1,12);a.B(10,label+"row");
    }
}
