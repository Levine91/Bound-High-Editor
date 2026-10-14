using System;

using System.IO;

using System.Linq;

using System.Collections.Generic;



sealed class EnemyDefinition {

    public int stage {get;set;}

    public int kind {get;set;}

    public int[] strip {get;set;}

}

sealed class EnemyMetadata {

    public EnemyDefinition[] enemies {get;set;}

    public Dictionary<string,int[]> free {get;set;}

    public Dictionary<string,StageInfo> converted {get;set;}

    public Dictionary<string,int[]> convertedFree {get;set;}

}



// Metadata contains descriptor numbers and ROM references, never ripped pixel data.

static class EnemyPatch {

    internal static readonly EnemyMetadata Meta=Load();
    internal const int PatchStart=0x19c784,PatchEnd=0x1ffde0;
    const int PatchAddress=0x07000000+PatchStart;

    static EnemyMetadata Load() {

        using(var r=new StreamReader(typeof(Document).Assembly.GetManifestResourceStream("enemies.json")))

            return Document.Json.Deserialize<EnemyMetadata>(r.ReadToEnd());

    }

    internal static readonly string[] Names={"Pachin","Jellyfish","Jellyfish E","Dragonfly","Dragonfly E","Smiley","Turtle","Squid","Frog","Crab","Rock","Kite","Honeybee","Mask","Large Turtle","Billye"};
    internal static int Global(Document doc,StageInfo stage,int code) {
        if(code==0 || code>=6)return code;
        if(code==1)return 6;if(code>=4)return code+16;
        int variant=BitConverter.ToUInt16(doc.Rom,doc.Offset(stage.id)-32);
        if(code==3)return stage.World==1?(variant==0?8:10):12;
        return stage.World==1?(variant==0?7:9):stage.World==2?(variant==0?13:variant==1?11:14):stage.World==3?(variant==0?16:variant==1?15:17):(variant==0?19:18);
    }
    internal static string Name(Document doc,StageInfo stage,int code) {
        if(code==0)return "None";
        if(stage.Pocket)return "Ball";
        if(code<6 && doc.RenderInfo(stage).mode==2)return code==2?"Plasma Ball +":"Plasma Ball -";
        return Names[Global(doc,stage,code)-6];
    }
    internal static int Kind(int code) {return code<6?code:Meta.enemies[code-6].kind;}
    internal static StageInfo Donor(int code) {return Document.Info.Single(s=>s.id==Meta.enemies[code-6].stage);}

    internal static bool Imported(int[] cells) {return cells.Any(v=>(v>>7)>=6);}

    internal static StageInfo TerrainDonor(Document doc,StageInfo s,int type) {
        if(s.Pocket)return s;
        int id=s.id;
        if(type==32 || type==33)id=43;
        if(type==35 && s.scriptedTiles!=null)id=0;
        if(s.mode!=0 && (type==16 || (type>=26 && type<=31) || type==34 || type==35))id=0;
        if(type>=36 && type<=39)id=7;
        if(id==s.id)return s;
        var donor=Document.Info.Single(t=>t.id==id);
        // Keep native references when both banks already contain the same graphics.
        for(int i=0;i<16;i++) {
            int a=s.tiles[type.ToString()][i],b=donor.tiles[type.ToString()][i];
            if((a&~2047)!=(b&~2047))return donor;
            int pa=doc.Asset(s.banks[(a&2047)/1024]+(a&1023)*16),pb=doc.Asset(donor.banks[(b&2047)/1024]+(b&1023)*16);
            for(int j=0;j<16;j++)if(doc.Rom[pa+j]!=doc.Rom[pb+j])return donor;
        }
        return s;
    }
    static int TerrainPointer(byte[] rom,int type) {
        int index=BitConverter.ToUInt16(rom,0x19c0b8+type*2);
        return (type>=20 && type<=25?0x05000858:type>=26 && type<=34?0x05000f68:0x050012f4)+index*4;
    }
    static int OriginalPointer(byte[] rom,int address) {
        int at=address<0x05000f68?0x19b7dc+address-0x05000858:address<0x050012f4?0x19beec+address-0x05000f68:0x19c278+address-0x050012f4;
        return BitConverter.ToInt32(rom,at);
    }
    static bool TerrainChanged(Document doc,StageInfo s) {return doc.Cells(s.id).Any(v=>TerrainDonor(doc,doc.RenderInfo(s),v&127).id!=s.id);}
    internal static byte[] Apply(Document doc,byte[] original) {

        var changed=Document.AllStages.Where(s=>Imported(doc.Cells(s.id)) || doc.Regular(s.id) || TerrainChanged(doc,s) || doc.Setting(doc.Data.WindStrength,s.id)!=-1 || doc.Setting(doc.Data.Wind,s.id)!=-1 || doc.Setting(doc.Data.Music,s.id)!=-1 || doc.Setting(doc.Data.Gobbler,s.id)!=-1 || Enumerable.Range(0,4).Any(i=>doc.Timer(s.id,i)>=0) || Enumerable.Range(0,2).Any(i=>doc.BackgroundChoice(s.id,i)!=-1) || Enumerable.Range(0,160).Any(i=>doc.ElectricChoice(s.id,i)>=0 || doc.HiddenChoice(s.id,i)>=0)).ToArray();

        if(changed.Length==0)return original;

        // Verified padding in the supported ROM; preserve the header and vectors.
        if(original.Length!=2097152 || !Proto2.Empty(original,PatchStart,PatchEnd,doc.IsProto2))
            throw new InvalidDataException("The ROM patch area is not empty; export stopped to protect existing data.");
        var rom=(byte[])original.Clone();

        const int baseAddress=PatchAddress,table=baseAddress,ai=baseAddress+400;
        const int modes=baseAddress+1200,wind=baseAddress+1600,music=baseAddress+2000,gobbler=baseAddress+2400;
        const int windFirst=baseAddress+2800,windRepeat=baseAddress+3200,gobblerFirst=baseAddress+3600,gobblerRepeat=baseAddress+4000;
        const int electric=baseAddress+4400,hidden=baseAddress+7600,strength=baseAddress+23600;
        var payload=new List<byte>(new byte[24000]);
        var sharedBlocks=new Dictionary<string,int>();
        for(int i=4400;i<23600;i++)payload[i]=255;
        for(int sid=0;sid<91;sid++){
            for(int slot=0;slot<8;slot++)payload[400+sid*8+slot]=(byte)sid;
            for(int i=0;i<4;i++)Put(payload,2800+i*400+sid*4,doc.Timer(sid,i));
            Put(payload,1600+sid*4,doc.Setting(doc.Data.Wind,sid)>=0?doc.Setting(doc.Data.Wind,sid):doc.Setting(doc.Data.WindStrength,sid)>=0?(new[]{13,15,24,25,33,37,42,45}.Contains(sid)?1:0):-1);
            Put(payload,2000+sid*4,doc.Setting(doc.Data.Music,sid));
            Put(payload,2400+sid*4,doc.Setting(doc.Data.Gobbler,sid));
            Put(payload,23600+sid*4,sid>=71 && (doc.Setting(doc.Data.Wind,sid)==1 || doc.Setting(doc.Data.WindStrength,sid)>=0)?doc.Strength(sid):-1);
        }
        foreach(var stage in Document.CombatStages){
            Put(payload,1200+stage.id*4,Imported(doc.Cells(stage.id)) || doc.Regular(stage.id)?1:0);
            for(int i=0;i<160;i++){int value=doc.HiddenChoice(stage.id,i);payload[7600+stage.id*160+i]=unchecked((byte)(value<0?-1:value-20));}
        }

        foreach(var sourceStage in changed) {

            var stage=doc.RenderInfo(sourceStage);

            byte[] bank=new byte[32768];

            for(int k=0;k<2;k++)Array.Copy(doc.Rom,doc.Asset(stage.banks[k]),bank,k*16384,16384);

            var glyphs=new Dictionary<string,int>();

            for(int i=0;i<2048;i++)glyphs[Convert.ToBase64String(bank,i*16,16)]=i;

            var free=new Queue<int>((stage.Pocket || stage.ScoreAttack)?stage.freeGlyphs:(doc.Regular(stage.id)?Meta.convertedFree:Meta.free)[stage.id.ToString()]);

            var stream=new List<byte>();int slot=0,smallSlot=0;
            for(int x=0;x<16;x++)for(int y=0;y<10;y++){
                int cell=y*16+x,kind=Kind(doc.Cells(stage.id)[cell]>>7);if(kind==0)continue;
                int index=kind==1?smallSlot++:16+slot;
                if(!stage.Pocket)payload[4400+stage.id*32+index]=unchecked((byte)doc.ElectricChoice(stage.id,cell));
                if(kind>1)slot+=kind>=4?2:1;
            }
            slot=0;
            for(int plane=0;plane<2;plane++){
                int choice=doc.BackgroundChoice(stage.id,plane);if(choice==-1)continue;
                var donor=choice<0?stage:Document.AllStages.Single(t=>t.id==choice);
                byte[] data=choice==-2?new byte[8192]:Convert.FromBase64String(donor.backgrounds[plane]);
                for(int i=0;i<4096;i++){
                    int d=choice==-2?2047:BitConverter.ToUInt16(data,i*2),chr=d&2047,target;
                    string key=choice==-2?Convert.ToBase64String(new byte[16]):Convert.ToBase64String(doc.Rom,doc.Asset(donor.banks[chr/1024]+chr%1024*16),16);
                    if(!glyphs.TryGetValue(key,out target)){
                        if(free.Count==0)throw new InvalidDataException(stage+": this background/enemy mix exceeds character memory. Choose another background or fewer enemy varieties.");
                        target=free.Dequeue();glyphs[key]=target;Block(payload,sharedBlocks,stream,0x78000+target*16,Convert.FromBase64String(key));
                    }
                    ushort mapped=(ushort)((d&~2047)|target);data[i*2]=(byte)mapped;data[i*2+1]=(byte)(mapped>>8);
                }
                Block(payload,sharedBlocks,stream,plane==0?0x2a000:0x28000,data);
            }
            var patchedPointers=new HashSet<int>();
            foreach(int type in doc.Cells(stage.id).Select(v=>v&127).Distinct()) {
                var donor=TerrainDonor(doc,stage,type);if(donor.id==stage.id)continue;
                int address=doc.IsProto2?Proto2.TerrainPointer(doc.Rom,type):TerrainPointer(doc.Rom,type);if(!patchedPointers.Add(address))continue;
                var descriptors=new ushort[16];
                for(int i=0;i<16;i++) {
                    int d=donor.tiles[type.ToString()][i],chr=d&2047,source=doc.Asset(donor.banks[chr/1024]+chr%1024*16),target;
                    string key=Convert.ToBase64String(doc.Rom,source,16);
                    if(!glyphs.TryGetValue(key,out target)) {
                        if(free.Count==0)throw new InvalidDataException(stage+": this tile/enemy mix exceeds character memory.");
                        target=free.Dequeue();glyphs[key]=target;var pixels=new byte[16];Array.Copy(doc.Rom,source,pixels,0,16);Block(payload,sharedBlocks,stream,0x78000+target*16,pixels);
                    }
                    descriptors[i]=(ushort)((d&~2047)|target);
                }
                int pointer=baseAddress+payload.Count;var data=new byte[32];Buffer.BlockCopy(descriptors,0,data,0,32);payload.AddRange(data);
                Block(payload,sharedBlocks,stream,address,BitConverter.GetBytes(pointer));
            }

            // The game's allocator walks columns first.

            for(int x=0;x<16;x++)for(int y=0;y<10;y++) {

                int code=doc.Cells(stage.id)[y*16+x]>>7,kind=Kind(code);

                if(kind<2)continue;

                if(code>=6) {

                    var enemy=Meta.enemies[code-6];var donor=Donor(code);

                    payload[400+stage.id*8+slot]=(byte)enemy.stage;

                    int width=kind>=4?16:8;

                    var descriptors=new ushort[enemy.strip.Length];

                    for(int i=0;i<descriptors.Length;i++) {

                        int d=enemy.strip[i],chr=d&2047,source=doc.Asset(donor.banks[chr/1024]+chr%1024*16);

                        string key=Convert.ToBase64String(doc.Rom,source,16);int target;

                        if(!glyphs.TryGetValue(key,out target)) {

                            if(free.Count==0)throw new InvalidDataException(stage+": this enemy mix exceeds available character memory. Remove an enemy variety.");

                            target=free.Dequeue();glyphs[key]=target;

                            var pixels=new byte[16];Array.Copy(doc.Rom,source,pixels,0,16);Block(payload,sharedBlocks,stream,0x78000+target*16,pixels);

                        }

                        descriptors[i]=(ushort)((d&~2047)|target);

                    }

                    for(int row=0;row<64;row++) {

                        var data=new byte[width*2];Buffer.BlockCopy(descriptors,row*width*2,data,0,data.Length);

                        Block(payload,sharedBlocks,stream,0x22000+(row*64+slot*8)*2,data);

                    }

                }

                slot+=kind>=4?2:1;

            }

            stream.AddRange(new byte[4]);
            Put(payload,stage.id*4,Share(payload,sharedBlocks,stream.ToArray()));

        }

        if(doc.IsProto2)return Proto2Patch.Apply(doc,rom,payload);
        var asm=new V810(baseAddress+payload.Count);

        int display=asm.Address;

        asm.I(17,-4,3);asm.I(55,3,31,0);asm.J(0x0703eb88,true);

        asm.I(41,3,3,-24);for(int r=10;r<=15;r++)asm.I(55,3,r,(r-10)*4);

        asm.Const(10,0x0500653c);asm.I(49,10,10,0);asm.I(19,0,10);asm.B(10,"restore");
        asm.Const(10,0x05006558);asm.I(49,10,10,0);asm.I(45,10,10,65535);

        asm.Const(11,91);asm.I(3,11,10);asm.B(9,"restore");
        asm.I(20,2,10);asm.Const(11,table);asm.I(1,11,10);asm.I(51,10,10,0);

        asm.I(19,0,10);asm.B(2,"restore");

        asm.Label("block");asm.I(51,10,11,0);asm.I(19,0,11);asm.B(2,"redraw");

        asm.I(51,10,12,4);asm.I(51,10,14,8);asm.I(17,12,10);

        asm.Label("copy");asm.I(49,14,13,0);asm.I(53,11,13,0);asm.I(17,2,14);asm.I(17,2,11);asm.I(17,-1,12);asm.B(10,"copy");

        asm.J("block");

        asm.Label("redraw");asm.J(0x070492f0,true);asm.Label("restore");for(int r=10;r<=15;r++)asm.I(51,3,r,(r-10)*4);

        asm.I(41,3,3,24);asm.I(51,3,31,0);asm.I(17,4,3);asm.I(6,31,0);

        int behavior=asm.Address;

        asm.I(49,12,10,0x6558);asm.I(17,-8,3);asm.I(55,3,11,0);asm.I(55,3,12,4);

        asm.Const(11,91);asm.I(3,11,10);asm.B(9,"aiReturn");

        asm.I(20,3,10);asm.I(1,28,10);asm.Const(11,ai);asm.I(1,11,10);asm.I(48,10,10,0);

        asm.Label("aiReturn");asm.I(51,3,11,0);asm.I(51,3,12,4);asm.I(17,8,3);asm.J(0x07010d42,false);

        int initialize=asm.Address;

        // r11 is the stage ID at the native mode dispatcher. Preserve its scratch register.

        asm.I(17,-4,3);asm.I(55,3,12,0);asm.Const(12,91);asm.I(3,12,11);asm.B(9,"nativeInit");

        asm.I(0,11,10);asm.I(20,2,10);asm.Const(12,modes);asm.I(1,12,10);asm.I(51,10,10,0);

        asm.I(19,0,10);asm.B(2,"nativeInit");asm.I(51,3,12,0);asm.I(17,4,3);asm.J(0x0706a084,false);

        asm.Label("nativeInit");asm.I(51,3,12,0);asm.I(17,4,3);asm.I(41,11,10,-5);asm.J(0x0706a078,false);

        int hiddenHook=asm.Address;asm.I(17,-4,3);asm.I(55,3,31,0);asm.J(0x070046ec,true);HiddenOverride(asm,hidden);asm.I(51,3,31,0);asm.I(17,4,3);asm.I(6,31,0);
        int windHook=asm.Address;
        asm.I(17,-4,3);asm.I(55,3,31,0);asm.J(0x07009dd8,true);
        asm.Const(10,0x05006558);asm.I(49,10,10,0);asm.I(45,10,10,65535);asm.Const(11,91);asm.I(3,11,10);asm.B(9,"windDone");
        asm.I(0,10,12);asm.I(20,2,12);asm.Const(11,strength);asm.I(1,11,12);asm.I(51,12,12,0);asm.I(19,-1,12);asm.B(2,"nativeStrength");asm.Const(11,0x05008d7e);asm.I(53,11,12,0);asm.Label("nativeStrength");
        asm.I(20,2,10);asm.Const(11,wind);asm.I(1,11,10);asm.I(51,10,10,0);asm.I(19,-1,10);asm.B(2,"windDone");
        asm.Const(11,0x050078d8);asm.I(53,11,10,0);asm.I(53,11,0,2);
        asm.Const(11,0x05008438);asm.I(53,11,0,0);asm.Const(11,0x05008b48);asm.I(53,11,0,0);asm.Const(11,0x050078a0);asm.I(53,11,0,0);
        asm.I(19,0,10);asm.B(2,"windDone");
        asm.Const(11,0x05008d7e);asm.I(49,11,10,0);asm.I(19,0,10);asm.B(10,"windStrength");asm.Const(10,28);asm.I(53,11,10,0);asm.Label("windStrength");
        asm.Const(11,0x0500843e);asm.Const(10,100);asm.I(53,11,10,0);
        asm.Label("windDone");asm.Const(10,0x0500843e);asm.I(49,10,11,0);TimerOverride(asm,windFirst);asm.I(53,10,11,0);asm.I(51,3,31,0);asm.I(17,4,3);asm.I(6,31,0);
        int gobblerHook=asm.Address;
        asm.I(17,-4,3);asm.I(55,3,31,0);asm.J(0x0702b5d8,true);
        asm.Const(10,0x05006558);asm.I(49,10,10,0);asm.I(45,10,10,65535);asm.Const(11,91);asm.I(3,11,10);asm.B(9,"gobblerDone");
        asm.I(20,2,10);asm.Const(11,gobbler);asm.I(1,11,10);asm.I(51,10,10,0);asm.I(19,-1,10);asm.B(2,"gobblerDone");
        asm.Const(11,0x05007fe0);asm.I(53,11,10,0);asm.I(19,0,10);asm.B(2,"gobblerDone");
        // Preserve positive native header timing, supplying header 20 only where absent.
        asm.Const(11,0x05001fb8);asm.I(49,11,10,0);asm.I(19,0,10);asm.B(15,"gobblerDone");
        asm.Const(10,500);asm.I(53,11,10,0);asm.Const(12,0x05006dbe);asm.I(49,12,12,0);asm.I(16,3,13);asm.I(2,12,13);asm.I(8,13,10);asm.I(23,1,10);asm.I(53,11,10,4);
        asm.Label("gobblerDone");asm.Const(10,0x05001fbc);asm.I(49,10,11,0);TimerOverride(asm,gobblerFirst);asm.I(53,10,11,0);asm.I(51,3,31,0);asm.I(17,4,3);asm.I(6,31,0);
        int musicHook=asm.Address;
        asm.I(17,-8,3);asm.I(55,3,10,0);asm.I(55,3,11,4);
        asm.Const(10,0x05006558);asm.I(49,10,10,0);asm.I(45,10,10,65535);asm.Const(11,91);asm.I(3,11,10);asm.B(9,"nativeMusic");
        asm.I(20,2,10);asm.Const(11,music);asm.I(1,11,10);asm.I(51,10,10,0);asm.I(19,-1,10);asm.B(2,"nativeMusic");
        asm.I(0,10,6);asm.Const(11,25);asm.I(3,11,6);asm.B(9,"effectMusic");
        asm.I(51,3,10,0);asm.I(51,3,11,4);asm.I(17,8,3);asm.J(0x07055678,false);
        asm.Label("effectMusic");asm.I(41,6,6,-25);asm.I(17,-8,3);asm.I(55,3,31,0);asm.I(55,3,6,4);asm.I(16,0,6);asm.J(0x07055678,true);
        asm.I(51,3,31,0);asm.I(51,3,6,4);asm.I(17,8,3);asm.I(51,3,10,0);asm.I(51,3,11,4);asm.I(17,8,3);asm.I(16,15,7);asm.I(16,15,8);asm.J(0x070555bc,false);
        asm.Label("nativeMusic");asm.I(51,3,10,0);asm.I(51,3,11,4);asm.I(17,8,3);asm.I(41,3,3,-20);asm.J(0x07033fa0,false);
        int updateHook=asm.Address;
        asm.Const(10,0x05006558);asm.I(49,10,10,0);asm.I(45,10,10,65535);asm.Const(11,91);asm.I(3,11,10);asm.B(9,"nativeUpdate");
        asm.I(20,2,10);asm.Const(11,modes);asm.I(1,10,11);asm.I(51,11,11,0);asm.I(19,0,11);asm.B(10,"regularUpdate");
        asm.Const(11,0x05006de4);asm.I(49,11,11,0);asm.I(19,2,11);asm.B(10,"nativeUpdate");
        // Plasma already updates large actors, but omits wind and timeout ticks.
        asm.I(17,-4,3);asm.I(55,3,10,0);
        asm.Const(11,wind);asm.I(1,10,11);asm.I(51,11,11,0);asm.I(19,1,11);asm.B(10,"plasmaGobbler");asm.J(0x0701428c,true);
        asm.Label("plasmaGobbler");asm.I(51,3,10,0);asm.Const(11,gobbler);asm.I(1,10,11);asm.I(51,11,11,0);asm.I(19,1,11);asm.B(10,"plasmaDone");asm.J(0x0702b6c4,true);
        asm.Label("plasmaDone");asm.I(17,4,3);
        asm.Label("nativeUpdate");asm.Const(10,0x05000000);asm.J(0x07073b66,false);
        asm.Label("regularUpdate");asm.J(0x07073b74,false);
        int terrainHook=asm.Address;
        asm.I(17,-4,3);asm.I(55,3,31,0);
        foreach(int address in Enumerable.Range(1,43).Select(t=>TerrainPointer(doc.Rom,t)).Distinct()) {
            asm.Const(10,address);asm.Const(11,OriginalPointer(doc.Rom,address));asm.I(55,10,11,0);
        }
        asm.J(0x070492f0,true);asm.I(51,3,31,0);asm.I(17,4,3);asm.I(6,31,0);
        int windRepeatHook=asm.Address;TimerOverride(asm,windRepeat);asm.I(53,29,11,-0x7bc2);asm.I(6,31,0);
        int gobblerRepeatHook=asm.Address;TimerOverride(asm,gobblerRepeat);asm.I(53,10,11,0x1fbc);asm.I(6,31,0);
        int electricDispatch=asm.Address;
        asm.I(17,-8,3);asm.I(55,3,10,0);asm.I(55,3,11,4);
        ElectricOverride(asm,electric,10,23,false,16,false);
        asm.I(19,-1,10);asm.B(2,"electricDispatchDone");
        // Only the ordinary and two double-slot Adventure types; exclude timeout/boss actors.
        asm.I(19,1,14);asm.B(15,"electricLargeCheck");asm.J("electricKind");
        asm.Label("electricLargeCheck");asm.I(19,10,14);asm.B(2,"electricKind");asm.I(19,12,14);asm.B(10,"electricDispatchDone");
        asm.Label("electricKind");asm.I(19,1,10);asm.B(10,"electricOff");asm.I(16,1,14);asm.J("electricDispatchDone");
        asm.Label("electricOff");asm.I(19,1,14);asm.B(10,"electricDispatchDone");asm.I(16,0,14);
        asm.Label("electricDispatchDone");asm.I(51,3,10,0);asm.I(51,3,11,4);asm.I(17,8,3);asm.Const(12,0x07070000);asm.J(0x0706e5e4,false);
        var electricHooks=new List<int[]>();
        foreach(var hook in new[]{new[]{0x6e308,17,29,1,0},new[]{0x21bfe,10,28,0,0},new[]{0x21c9e,10,28,0,0},new[]{0x6e7f0,14,23,0,16},new[]{0x6e8de,14,23,0,16},new[]{0x6ea34,14,23,0,16},new[]{0x6eb4a,14,23,0,16},new[]{0x6ec86,14,23,0,16},new[]{0x6f4a8,15,23,0,16}}){
            int address=asm.Address;ElectricOverride(asm,electric,hook[1],hook[2],hook[3]==1,hook[4],true);asm.J(0x07000000+hook[0]+4,false);electricHooks.Add(new[]{hook[0],address});
        }
        payload.AddRange(asm.Finish());

        int capacity=(doc.Data.StageHud?StageHudPatch.Start(doc.Data.BossRush):doc.Data.BossRush?BossRushPatch.Start:PatchEnd)-PatchStart;
        if(payload.Count>capacity)throw new InvalidDataException("These edits need "+payload.Count.ToString("N0")+" bytes of patch space; the original-size ROM has "+capacity.ToString("N0")+" bytes available. Reduce imported graphics or background combinations. No ROM was exported.");

        payload.CopyTo(rom,PatchStart);

        Hook(rom,0x5a89c,display,true,doc.Rom);Hook(rom,0x10d3e,behavior,false,doc.Rom);

        Hook(rom,0x6a112,hiddenHook,true,doc.Rom);
        Hook(rom,0x6a074,initialize,false,doc.Rom);Hook(rom,0x6a10a,windHook,true,doc.Rom);Hook(rom,0x33f9c,musicHook,false,doc.Rom);Hook(rom,0x73b62,updateHook,false,doc.Rom);Hook(rom,0x5a898,terrainHook,true,doc.Rom);
        Hook(rom,0x6a10e,gobblerHook,true,doc.Rom);
        Hook(rom,0x1444a,windRepeatHook,true,doc.Rom);Hook(rom,0x14486,windRepeatHook,true,doc.Rom);Hook(rom,0x2b7ce,gobblerRepeatHook,true,doc.Rom);

        Hook(rom,0x6e5e0,electricDispatch,false,doc.Rom);
        foreach(var hook in electricHooks)Hook(rom,hook[0],hook[1],false,doc.Rom);
        return rom;

    }

    // ROM-only per-stage slot overrides: -1 inherits native Expert setting.
    static void ElectricOverride(V810 asm,int table,int result,int index,bool smallPointer,int offset,bool inherit){
        string done="electric"+asm.Address,original=done+"original";
        int[] saved=new[]{10,11,12,30}.Where(r=>r!=result).ToArray();
        asm.I(41,3,3,-saved.Length*4);for(int i=0;i<saved.Length;i++)asm.I(55,3,saved[i],i*4);
        // Copy the slot before touching scratch registers (small contact uses r29+24).
        asm.I(0,index,12);
        if(smallPointer){asm.Const(10,0x05008b6c);asm.I(2,10,12);asm.Const(10,26);asm.I(9,10,12);}
        if(offset!=0)asm.I(41,12,12,offset);
        asm.Const(10,0x05006558);asm.I(49,10,10,0);asm.I(45,10,10,65535);asm.Const(11,91);asm.I(3,11,10);asm.B(9,original);
        asm.I(20,5,10);asm.I(1,12,10);asm.Const(11,table);asm.I(1,11,10);asm.I(48,10,11,0);asm.I(19,-1,11);asm.B(2,original);asm.I(0,11,result);asm.J(done);
        asm.Label(original);if(inherit){asm.Const(result,0x05006562);asm.I(49,result,result,0);}else asm.I(16,-1,result);
        asm.Label(done);for(int i=0;i<saved.Length;i++)asm.I(51,3,saved[i],i*4);asm.I(41,3,3,saved.Length*4);
    }

    // Apply explicit contents after native initialization/random placement. Other cells stay native.
    static void HiddenOverride(V810 asm,int table){
        asm.I(41,3,3,-24);for(int r=10;r<=15;r++)asm.I(55,3,r,(r-10)*4);
        asm.Const(10,0x05006de4);asm.I(49,10,10,0);asm.I(19,0,10);asm.B(10,"hiddenDone");
        asm.Const(10,0x0500653c);asm.I(49,10,10,0);asm.I(19,0,10);asm.B(10,"hiddenDone");
        asm.Const(10,0x05006558);asm.I(49,10,10,0);asm.I(45,10,10,65535);asm.Const(11,91);asm.I(3,11,10);asm.B(9,"hiddenDone");
        asm.I(0,10,11);asm.I(20,7,10);asm.I(20,5,11);asm.I(1,11,10);asm.Const(11,table);asm.I(1,11,10);
        asm.Const(11,0x05008780);asm.Const(12,160);
        asm.Label("hiddenLoop");asm.I(48,10,13,0);asm.I(19,-1,13);asm.B(2,"hiddenNext");asm.I(53,11,13,0);
        asm.Label("hiddenNext");asm.I(17,1,10);asm.I(17,2,11);asm.I(17,-1,12);asm.I(19,0,12);asm.B(10,"hiddenLoop");
        asm.Label("hiddenDone");for(int r=10;r<=15;r++)asm.I(51,3,r,(r-10)*4);asm.I(41,3,3,24);
    }

    // Replace a countdown in r11 only when the current stage has a custom value.
    // Keep the native base register and scratch register intact at store hooks.
    static void TimerOverride(V810 asm,int table) {
        string done="timer"+asm.Address;
        asm.I(17,-8,3);asm.I(55,3,10,0);asm.I(55,3,12,4);
        asm.Const(10,0x05006558);asm.I(49,10,10,0);asm.I(45,10,10,65535);asm.Const(12,91);asm.I(3,12,10);asm.B(9,done);
        asm.I(20,2,10);asm.Const(12,table);asm.I(1,12,10);asm.I(51,10,12,0);asm.I(19,-1,12);asm.B(2,done);asm.I(0,12,11);
        asm.Label(done);asm.I(51,3,10,0);asm.I(51,3,12,4);asm.I(17,8,3);
    }

    static void Block(List<byte> payload,Dictionary<string,int> shared,List<byte> stream,int address,byte[] data) {
        if(data.Length==0 || data.Length%4!=0)throw new InvalidDataException("Unaligned graphics block.");
        int pointer=Share(payload,shared,data);
        stream.AddRange(BitConverter.GetBytes(address));stream.AddRange(BitConverter.GetBytes(data.Length/2));stream.AddRange(BitConverter.GetBytes(pointer));
    }

    static int Share(List<byte> payload,Dictionary<string,int> shared,byte[] data) {
        string key=Convert.ToBase64String(data);int pointer;
        if(!shared.TryGetValue(key,out pointer)){
            pointer=PatchAddress+payload.Count;shared[key]=pointer;payload.AddRange(data);
        }
        return pointer;
    }

    static void Put(List<byte> bytes,int offset,int value) {var b=BitConverter.GetBytes(value);for(int i=0;i<4;i++)bytes[offset+i]=b[i];}

    static void Hook(byte[] rom,int offset,int target,bool link,byte[] source) {

        if(!rom.Skip(offset).Take(4).SequenceEqual(source.Skip(offset).Take(4)))throw new InvalidDataException("ROM hook conflict.");

        var a=new V810(0x07000000+offset);a.J(target,link);Array.Copy(a.Finish(),0,rom,offset,4);

    }

}



// Only the V810 instructions required by the three bounded hooks, with range checks.

sealed class V810 {

    readonly int start;readonly List<byte> bytes=new List<byte>();

    readonly Dictionary<string,int> labels=new Dictionary<string,int>();

    readonly List<Tuple<int,string,int>> fixups=new List<Tuple<int,string,int>>();

    internal V810(int address){start=address;}

    internal int Address {get{return start+bytes.Count;}}

    void H(int v){bytes.Add((byte)v);bytes.Add((byte)(v>>8));}

    internal void I(int op,int r1,int r2,int imm=0){if((op==17 || op==19) && (r1< -16 || r1>15))throw new InvalidDataException("Immediate out of range.");H((op<<10)|(r2<<5)|(r1&31));if(op>=40)H(imm);}

    internal void Const(int r,int value){I(47,0,r,(int)((uint)value>>16));I(44,r,r,value&65535);}

    internal void Label(string label){labels.Add(label,Address);}

    internal void B(int condition,string label){fixups.Add(Tuple.Create(bytes.Count,label,condition));H(0);}

    internal void J(string label){fixups.Add(Tuple.Create(bytes.Count,label,16));H(0);H(0);}

    internal void J(int target,bool link){int d=target-Address;if((d&1)!=0 || d< -33554432 || d>=33554432)throw new InvalidDataException("Jump out of range.");H(((link?43:42)<<10)|((d>>16)&1023));H(d);}

    internal byte[] Finish(){foreach(var f in fixups){int d=labels[f.Item2]-(start+f.Item1),v;

        if(f.Item3==16){v=(42<<10)|((d>>16)&1023);bytes[f.Item1+2]=(byte)d;bytes[f.Item1+3]=(byte)(d>>8);}

        else {if(d< -256 || d>254 || (d&1)!=0)throw new InvalidDataException("Branch out of range.");v=0x8000|(f.Item3<<9)|(d&511);}

        bytes[f.Item1]=(byte)v;bytes[f.Item1+1]=(byte)(v>>8);

    }return bytes.ToArray();}

}
