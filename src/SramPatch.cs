using System;
using System.IO;
using System.Linq;

// The supplied v1.0 patch targets Proto 2. Never apply its absolute addresses to BH.Debug.
static class SramPatch {
    internal static byte[] Apply(Document doc,byte[] input) {
        if(!doc.Data.Sram)return input;
        if(!doc.IsProto2)throw new InvalidDataException("SRAM Support requires the original Proto 2 ROM.");
        byte[] rom=BundledPatch.Apply(doc,input,"bound-save.ips");
        // The source patch moved New Game two bytes; keep its original caller correctly aligned.
        // Boss Rush supplies this dispatch itself when both enhancements are enabled.
        if(!doc.Data.BossRush)Proto2Patch.Hook(rom,doc.Rom,0x23b96,0x07023bda,false);
        if(doc.Data.BossRush) {
            const int start=0x3db08;
            if(!Proto2.Empty(doc.Rom,start,start+512,true))throw new InvalidDataException("SRAM compatibility patch space is occupied.");
            var a=new V810(0x07000000+start);Proto2Patch.Save(a);
            a.Const(10,Proto2BossRush.Flag);a.I(49,10,10);a.I(19,0,10);a.B(10,"skip");a.I(52,1,12,8);
            a.Label("skip");Proto2Patch.Restore(a);a.I(6,31,0);
            byte[] code=a.Finish();if(code.Length>512)throw new InvalidDataException("SRAM compatibility patch is too large.");Array.Copy(code,0,rom,start,code.Length);
            // Preserve earned Adventure progress while Boss Rush visits its boss stages.
            var hook=new V810(0x0703daf8);hook.J(0x07000000+start,true);Array.Copy(hook.Finish(),0,rom,0x3daf8,4);
        }
        return rom;
    }
}
