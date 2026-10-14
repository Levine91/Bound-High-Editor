using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

// Uses the original title font, boss stages, introductions and ending.
// Four KiB of verified ROM padding; RAM 05006566 is the unused halfword
// between the native demo flag (6564) and music pointer (6568).
// A set ending-complete flag is required before automatic title return.
static class BossRushPatch {
    internal const int Start=0x1fede0, Flag=0x05006566;
    internal static int UsedBytes;
    internal static byte[] Apply(Document doc,byte[] input) {
        if(!doc.Data.BossRush)return input;
        if(doc.IsProto2)return Proto2BossRush.Apply(doc,input);
        if(input.Length!=2097152 || input.Skip(Start).Take(EnemyPatch.PatchEnd-Start).Any(b=>b!=0))throw new InvalidDataException("Boss Rush patch space is occupied.");
        byte[] rom=(byte[])input.Clone();var data=new List<byte>();
        int frame=0x07000000+Start;
        data.AddRange(doc.Rom.Skip(0xbb5ae).Take(7*46));
        data.AddRange(doc.Rom.Skip(0xbb5ae+46).Take(46));
        data.AddRange(doc.Rom.Skip(0xbb5ae+7*46).Take(46));
        int name=frame+data.Count;
        foreach(int glyph in new[]{0x7ff,0x7ff,0x7ff,0x7ff,0x7ff,0x219,0x1fb,0x1f2,0x1f2,0x7ff,0x1f7,0x1f0,0x1f2,0x233,0x7ff,0x7ff,0x7ff,0x7ff,0x7ff})data.AddRange(BitConverter.GetBytes((ushort)glyph));
        var a=new V810(frame+data.Count);
        int draw=a.Address;
        Save(a);a.I(0,6,20);
        Draw(a,36,23,9,frame,0);
        Draw(a,50,21,1,0x070bb584,0);
        Draw(a,51,30,1,0x070bbb0e,0);
        int[] rows={0x070bbb96,0x070bbb70,0x070bbb4a,0x070bbae8,name};
        for(int i=0;i<5;i++) {
            a.I(16,0,10);a.I(19,i,20);a.B(2,"bright"+i);a.I(16,2,10);a.Label("bright"+i);
            Draw(a,45+i,19,1,rows[i],-1);
        }
        foreach(int b in new[]{0x050040fc,0x05001fc0}) {
            a.Const(11,b);a.I(41,0,10,408);a.I(53,11,10,0x24e);a.I(41,0,10,400);a.I(53,11,10,0x1c6);a.I(41,0,10,71);a.I(53,11,10,0x20e);
            a.I(41,0,10,39);a.I(53,11,10,0x1ec);
            a.I(0,20,10);a.I(20,3,10);a.I(41,10,10,160);a.I(53,11,10,0x1c0);
        }
        Restore(a);a.I(6,31,0);
        int open=a.Address;
        a.I(53,27,10,0x1dc);a.I(53,27,0,0x220);a.J(0x0702da3e,false);
        int close=a.Address;
        a.I(53,27,0,0x1dc);a.I(41,0,10,unchecked((short)0xc000));a.I(53,27,10,0x220);a.J(0x0702d92e,false);
        int initial=a.Address;
        Save(a);a.J(0x0702c3a4,true);a.I(51,3,6,24);a.J(draw,true);Restore(a);a.I(6,31,0);
        int navigate=a.Address;
        Save(a);a.I(51,3,6,140);a.J(draw,true);Restore(a);
        a.I(51,3,14,12);a.I(55,3,14,32);a.J(0x0702dcec,false);
        int choose=a.Address;
        a.Const(10,Flag);a.I(53,10,0);a.I(51,3,12,12);a.I(19,4,12);a.B(10,"ordinary");
        a.I(16,1,11);a.I(53,10,11);a.Const(10,0x05000000);a.I(52,10,0,0x1981);a.I(52,10,0,0x19a8);
        a.I(16,4,11);a.I(52,28,11);a.I(55,3,0,24);a.I(16,1,12);a.J(0x0702d71a,false);
        a.Label("ordinary");a.J(0x0702dd00,false);
        int begin=a.Address;
        a.Const(10,Flag);a.I(49,10,10);a.I(19,0,10);a.B(2,"normalBegin");
        a.Const(10,0x05000000);a.I(16,10,11);a.I(52,10,11,0x1984);
        foreach(int offset in new[]{0x198d,0x1982,0x19a3})a.I(52,10,0,offset);
        a.I(53,10,0,0x6db8);a.I(16,-1,11);
        foreach(int offset in new[]{0x19a4,0x19a5,0x19a6})a.I(52,10,11,offset);
        a.J(0x0702d836,false);
        a.Label("normalBegin");a.I(51,3,26,0x5c);a.J(0x0702eaba,false);
        int advance=a.Address;
        a.Const(10,Flag);a.I(49,10,10);a.I(19,0,10);a.B(2,"normalAdvance");
        int[] bosses={10,22,34,47,49};
        for(int i=0;i<4;i++) {a.Const(10,bosses[i]);a.I(3,10,11);a.B(10,"next"+i);a.Const(10,bosses[i+1]);a.I(53,22,10,0x6558);a.J(0x07033536,false);a.Label("next"+i);}
        a.Label("normalAdvance");a.I(41,11,10,1);a.J(0x0703351e,false);
        int finishEnding=a.Address;
        a.Const(13,Flag);a.I(49,13,13);a.I(19,0,13);a.B(2,"normalEndingWait");
        a.I(51,3,13,0x30);a.I(19,0,13);a.B(2,"normalEndingWait");
        a.I(55,3,0,0x1c);a.J(0x0701856e,false);
        a.Label("normalEndingWait");a.I(47,0,13,0x303c);a.J(0x0701853e,false);
        int cancel=a.Address;
        a.Const(10,Flag);a.I(49,10,10);a.I(19,0,10);a.B(2,"normalCancel");a.J("resetTitle");
        a.Label("normalCancel");a.I(49,27,10,0x154);a.J(0x0702db24,false);
        int entry=a.Address;
        a.Const(10,Flag);a.I(49,10,11);a.I(19,0,11);a.B(2,"normalEntry");
        a.Const(10,0x05006db8);a.I(49,10,11);a.I(19,-1,11);a.B(2,"normalEntry");a.J("resetTitle");
        a.Label("normalEntry");a.I(47,0,10,0x500);a.J(0x0702d3a4,false);
        int ending=a.Address;
        a.Const(14,Flag);a.I(49,14,10);a.I(19,0,10);a.B(2,"normalEnding");
        a.J(0x070182a8,true);a.I(16,0,6);a.J(0x07055678,true);
        a.Label("resetTitle");a.Const(10,0x05000000);a.I(53,10,0,0x6566);a.I(53,10,0,0x6db8);
        foreach(int offset in new[]{0x1981,0x1982,0x198d,0x19a0,0x19a8,0x19a9})a.I(52,10,0,offset);
        a.I(16,-1,11);a.I(52,10,11,0x1984);a.Const(21,0x05002420);a.J(0x0702d3a8,false);
        a.Label("normalEnding");a.I(47,0,14,0x500);a.J(0x0702e776,false);
        data.AddRange(a.Finish());UsedBytes=data.Count;
        if(data.Count>EnemyPatch.PatchEnd-Start)throw new InvalidDataException("Boss Rush exceeds its reserved space.");
        data.CopyTo(rom,Start);
        Hook(rom,doc.Rom,0x2da3a,open,false);Hook(rom,doc.Rom,0x2d92a,close,false);
        Hook(rom,doc.Rom,0x1853a,finishEnding,false);Hook(rom,doc.Rom,0x2d3a0,entry,false);Hook(rom,doc.Rom,0x2db20,cancel,false);
        Hook(rom,doc.Rom,0x2d648,initial,true);Hook(rom,doc.Rom,0x2dc02,navigate,false);
        Hook(rom,doc.Rom,0x2dcfc,choose,false);Hook(rom,doc.Rom,0x2eab6,begin,false);
        Hook(rom,doc.Rom,0x3351a,advance,false);Hook(rom,doc.Rom,0x2e772,ending,false);
        var limit=new V810(0x0702dd78);limit.I(19,3,11);Array.Copy(limit.Finish(),0,rom,0x2dd78,2);
        return rom;
    }
    static void Draw(V810 a,int y,int width,int height,int pointer,int palette) {
        if(palette>=0)a.I(16,palette,10);
        a.I(55,3,10,8);a.Const(10,pointer);a.I(55,3,10,4);a.I(16,height,10);a.I(55,3,10,0);
        a.I(16,0,6);a.I(16,0,7);a.I(41,0,8,y);a.I(41,0,9,width);a.J(0x07069b38,true);
    }
    // 16 outgoing-argument bytes, then all potentially live registers r4..r31.
    static void Save(V810 a) {a.I(41,3,3,-128);for(int r=4;r<32;r++)a.I(55,3,r,16+(r-4)*4);}
    static void Restore(V810 a) {for(int r=4;r<32;r++)a.I(51,3,r,16+(r-4)*4);a.I(41,3,3,128);}
    static void Hook(byte[] rom,byte[] original,int offset,int target,bool link) {
        if(!rom.Skip(offset).Take(4).SequenceEqual(original.Skip(offset).Take(4)))throw new InvalidDataException("Boss Rush hook conflict.");
        var a=new V810(0x07000000+offset);a.J(target,link);Array.Copy(a.Finish(),0,rom,offset,4);
    }
}
