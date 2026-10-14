using System;

using System.IO;

using System.Linq;

using System.Drawing;

using System.Drawing.Drawing2D;

using System.Drawing.Imaging;

using System.Collections.Generic;

using System.Security.Cryptography;

using System.Web.Script.Serialization;

using System.Windows.Forms;



sealed class EnemyPose {

    public int width { get; set; }

    public int height { get; set; }

    public int[][] parts { get; set; }

}

sealed class StageInfo {

    public int id { get; set; }

    public int[] banks { get; set; }

    public int[] types { get; set; }

    public int[] objects { get; set; }

    public int mode { get; set; }

    public Dictionary<string,int[]> tiles { get; set; }

    public Dictionary<string,int[]> scriptedTiles { get; set; }

    public int[] palettes { get; set; }

    public Dictionary<string,int[]> enemies { get; set; }

    public string[] backgrounds { get; set; }
    public int[] freeGlyphs {get;set;}

    public Dictionary<string,EnemyPose> enemyPoses { get; set; }

    public int artWorld {get;set;}
    internal bool ScoreAttack {get{return id>=71 && id<=90;}}
    internal bool Pocket {get{return id>=50 && id<70;}}
    internal int World { get { return ScoreAttack?artWorld:Math.Min(4,id/12+1); } }

    internal int Number { get { return id-(World-1)*12+1; } }

    public override string ToString() { return ScoreAttack?"Score Attack!! · Stage "+(id-70):Pocket?"Pocket and Cushion · Stage "+(id-49):"World "+World+" · " + (mode == 0 ? "Stage " : mode == 2 ? "Plasma " : "Bonus ") + Number; }

}

sealed class Project {

    public int Version { get; set; }
    public bool BossRush {get;set;}
    public bool StageHud {get;set;}
    public bool Sram {get;set;}
    public bool DebugRestoration {get;set;}

    public string BaseSha1 { get; set; }

    public Dictionary<string,int[]> Stages { get; set; }
    public Dictionary<string,int[]> PocketStages {get;set;}
    public Dictionary<string,int[]> PocketRules {get;set;}
    public Dictionary<string,int[]> ScoreStages {get;set;}
    public Dictionary<string,int> ScoreTargets {get;set;}

    public int[] RegularStages {get;set;}
    public Dictionary<string,int> Wind {get;set;}
    public Dictionary<string,int> WindStrength {get;set;}
    public Dictionary<string,int> Music {get;set;}
    public Dictionary<string,int> Gobbler {get;set;}
    public Dictionary<string,int[]> Timers {get;set;}
    public Dictionary<string,int[]> Backdrops {get;set;}
    public Dictionary<string,int[]> Electric {get;set;}
    public Dictionary<string,int[]> Hidden {get;set;}

}

sealed class Document {

    internal const string Hash = "05548676fd332e4898f8779b952b1244ff2103c2";

    internal static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 4000000 };

    internal static readonly StageInfo[] Info = LoadInfo();
    internal static readonly StageInfo[] PocketInfo=LoadInfo("pocket.json");
    internal static readonly StageInfo[] ScoreInfo=LoadInfo("score.json");
    internal static IEnumerable<StageInfo> CombatStages {get{return Info.Concat(ScoreInfo);}}
    internal static IEnumerable<StageInfo> AllStages {get{return CombatStages.Concat(PocketInfo);}}
    internal static readonly int[] PocketTerrain={0,2,29,30,31,32,34,36,37};

    internal byte[] Rom;

    internal string Source;
    internal string SourceHash;
    internal bool IsProto2 {get{return SourceHash==Proto2.Hash;}}
    internal int Asset(int address){return IsProto2?Proto2.Asset(address):address;}

    internal Project Data;

    static StageInfo[] LoadInfo(string resource="stages.json") {

        using (var r = new StreamReader(typeof(Document).Assembly.GetManifestResourceStream(resource)))

            return Json.Deserialize<StageInfo[]>(r.ReadToEnd());

    }

    internal static string Digest(byte[] b) { using (var h=SHA1.Create()) return BitConverter.ToString(h.ComputeHash(b)).Replace("-", "").ToLowerInvariant(); }

    internal int Offset(int id) { return (IsProto2?0x428b0:0xA9CFC) + id * 352; }

    internal Document(string path) {

        Source=Path.GetFullPath(path);

        if (new FileInfo(path).Length != 2097152) throw new InvalidDataException("Please open a supported original 2 MiB Bound High ROM.");

        Rom=File.ReadAllBytes(path);

        SourceHash=Digest(Rom);
        if (SourceHash!=Hash && SourceHash!=Proto2.Hash) throw new InvalidDataException("This ROM revision is not supported. Open the original BH.Debug.vb or Bound High (World) (Proto 2).vb, not an exported ROM.");

        Data=new Project {Version=18,BaseSha1=SourceHash,Stages=new Dictionary<string,int[]>()};

        Data.PocketStages=OriginalPocket();Data.ScoreStages=OriginalScore();
        foreach(var s in Info) Data.Stages[s.id.ToString()]=Enumerable.Range(0,160).Select(i=>(int)BitConverter.ToUInt16(Rom,Offset(s.id)+2*i)).ToArray();

    }

    Dictionary<string,int[]> OriginalScore(){return ScoreInfo.ToDictionary(s=>s.id.ToString(),s=>Enumerable.Range(0,160).Select(i=>(int)BitConverter.ToUInt16(Rom,Offset(s.id)+2*i)).ToArray());}
    internal int ScoreDefault(int id){return BitConverter.ToUInt16(Rom,Offset(id)-2)*100;}
    internal int ScoreTarget(int id){int value=Setting(Data.ScoreTargets,id);return value<0?ScoreDefault(id):value;}
    internal void SetScoreTarget(int id,int value){if(Data.ScoreTargets==null)Data.ScoreTargets=new Dictionary<string,int>();if(value==ScoreDefault(id))Data.ScoreTargets.Remove(id.ToString());else Data.ScoreTargets[id.ToString()]=value;}
    Dictionary<string,int[]> OriginalPocket(){return PocketInfo.ToDictionary(s=>s.id.ToString(),s=>Enumerable.Range(0,160).Select(i=>(int)BitConverter.ToUInt16(Rom,Offset(s.id)+2*i)).ToArray());}
    internal int PocketDefault(int id,int index){return BitConverter.ToUInt16(Rom,Offset(id)+(index==0?-6:-2))*(index==0?1:100);}
    internal int PocketRule(int id,int index){int[] values;return Data.PocketRules!=null && Data.PocketRules.TryGetValue(id.ToString(),out values)?values[index]:PocketDefault(id,index);}
    internal void SetPocketRules(int id,int seconds,int points){if(Data.PocketRules==null)Data.PocketRules=new Dictionary<string,int[]>();if(seconds==PocketDefault(id,0) && points==PocketDefault(id,1))Data.PocketRules.Remove(id.ToString());else Data.PocketRules[id.ToString()]=new[]{seconds,points};}
    internal int Setting(Dictionary<string,int> values,int id) {int value;return values!=null && values.TryGetValue(id.ToString(),out value)?value:-1;}
    internal void SetSetting(bool wind,int id,int value) {
        var values=wind?Data.Wind:Data.Music;if(values==null){values=new Dictionary<string,int>();if(wind)Data.Wind=values;else Data.Music=values;}
        if(value==-1)values.Remove(id.ToString());else values[id.ToString()]=value;
    }
    internal static readonly int[] Strengths={26,28,30,32,36};
    internal int Strength(int id){int v=Setting(Data.WindStrength,id);return v<0?(id>=71?28:BitConverter.ToUInt16(Rom,Offset(id)-2)):v;}
    internal void SetStrength(int id,int value){if(Data.WindStrength==null)Data.WindStrength=new Dictionary<string,int>();Data.WindStrength[id.ToString()]=value;}
    internal int[] Cells(int id) { return (id>=71?Data.ScoreStages:id>=50?Data.PocketStages:Data.Stages)[id.ToString()]; }
    internal void SetGobbler(int id,int value) {if(Data.Gobbler==null)Data.Gobbler=new Dictionary<string,int>();if(value<0)Data.Gobbler.Remove(id.ToString());else Data.Gobbler[id.ToString()]=value;}
    internal int Timer(int id,int index) {int[] values;return Data.Timers!=null && Data.Timers.TryGetValue(id.ToString(),out values)?values[index]:-1;}
    internal void SetTimers(int id,bool wind,int first,int repeat) {
        if(Data.Timers==null)Data.Timers=new Dictionary<string,int[]>();int[] values;
        if(!Data.Timers.TryGetValue(id.ToString(),out values))values=new[]{-1,-1,-1,-1};
        int index=wind?0:2;values[index]=first;values[index+1]=repeat;
        if(values.All(v=>v==-1))Data.Timers.Remove(id.ToString());else Data.Timers[id.ToString()]=values;
    }
    internal int BackgroundChoice(int id,int plane){int[] a;return Data.Backdrops!=null && Data.Backdrops.TryGetValue(id.ToString(),out a)?a[plane]:-1;}
    internal void SetBackground(int id,int plane,int value){if(Data.Backdrops==null)Data.Backdrops=new Dictionary<string,int[]>();int[] a;if(!Data.Backdrops.TryGetValue(id.ToString(),out a))a=new[]{-1,-1};a[plane]=value;if(a.All(v=>v==-1))Data.Backdrops.Remove(id.ToString());else Data.Backdrops[id.ToString()]=a;}
    internal int ElectricChoice(int id,int cell){int[] a;return Data.Electric!=null && Data.Electric.TryGetValue(id.ToString(),out a)?a[cell]:-1;}
    internal bool ElectricOn(StageInfo stage,int cell){int code=Cells(stage.id)[cell]>>7;return code!=0 && !(code<6 && RenderInfo(stage).mode!=0) && (ElectricChoice(stage.id,cell)>=0?ElectricChoice(stage.id,cell)==1:EnemyPatch.Kind(code)==3);}
    internal void SetElectric(int id,int cell,int value){if(Data.Electric==null)Data.Electric=new Dictionary<string,int[]>();int[] a;if(!Data.Electric.TryGetValue(id.ToString(),out a))a=Enumerable.Repeat(-1,160).ToArray();a[cell]=value;if(a.All(v=>v==-1))Data.Electric.Remove(id.ToString());else Data.Electric[id.ToString()]=a;}
    internal int HiddenChoice(int id,int cell){int[] a;return Data.Hidden!=null && Data.Hidden.TryGetValue(id.ToString(),out a)?a[cell]:-1;}
    internal void SetHidden(int id,int cell,int value){if(Data.Hidden==null)Data.Hidden=new Dictionary<string,int[]>();int[] a;if(!Data.Hidden.TryGetValue(id.ToString(),out a))a=Enumerable.Repeat(-1,160).ToArray();a[cell]=value;if(a.All(v=>v==-1))Data.Hidden.Remove(id.ToString());else Data.Hidden[id.ToString()]=a;}
    internal bool NativeGobbler(StageInfo s) {return s.ScoreAttack?s.id==87:s.id>11 && s.mode==0 && !new[]{18,27,38}.Contains(s.id) && BitConverter.ToInt16(Rom,Offset(s.id)-4)>0;}

    internal bool Regular(int id) {return Data.RegularStages!=null && Data.RegularStages.Contains(id);}

    internal StageInfo RenderInfo(StageInfo s) {return Regular(s.id)?EnemyPatch.Meta.converted[s.id.ToString()]:s;}

    internal void SetRegular(int id,bool value) {

        var ids=new HashSet<int>(Data.RegularStages??new int[0]);if(value)ids.Add(id);else ids.Remove(id);Data.RegularStages=ids.OrderBy(i=>i).ToArray();

    }

    internal string Serialize() { return Json.Serialize(Data); }

    internal void Load(string text) {

        var p=Json.Deserialize<Project>(text); Validate(p);

        if(p.Version==1) {

            foreach(var s in Info.Where(s=>s.id>=12))p.Stages[s.id.ToString()]=Enumerable.Range(0,160).Select(i=>(int)BitConverter.ToUInt16(Rom,Offset(s.id)+2*i)).ToArray();

            p.Version=2;

        }

        if(p.PocketStages==null)p.PocketStages=OriginalPocket();
        if(p.ScoreStages==null)p.ScoreStages=OriginalScore();
        p.Version=18;Data=p;

    }

    internal void Validate(Project p) {

        if(p==null || (p.Version<1 || p.Version>18 || (p.Version<14 && p.BossRush) || (p.Version<15 && p.StageHud) || (p.Sram && (p.Version<16 || !IsProto2)) || (p.DebugRestoration && (p.Version<17 || !IsProto2))) || p.BaseSha1!=SourceHash || p.Stages==null || p.Stages.Count!=(p.Version==1?11:Info.Length))

            throw new InvalidDataException("This is not a compatible Bound High Adventure project.");

        foreach(var s in Info.Where(s=>p.Version>=2 || s.id<12)) {

            int[] a;

            if(!p.Stages.TryGetValue(s.id.ToString(),out a) || a==null || a.Length!=160)

                throw new InvalidDataException("A stage must contain exactly 16 × 10 cells.");

            foreach(int v in a) if(v<0 || v>65535 || (v&127)>43 || (v>>7)>(p.Version>=3?21:5))

                throw new InvalidDataException(s+": unsupported tile or enemy type.");

        }

        if(p.Version>=13 && (p.ScoreStages==null || p.ScoreStages.Count!=20))throw new InvalidDataException("Score Attack requires all 20 stages.");
        if(p.ScoreStages!=null){if(p.Version<13 || p.ScoreStages.Count!=20)throw new InvalidDataException("Invalid Score Attack stages.");foreach(var stage in ScoreInfo){int[] a;if(!p.ScoreStages.TryGetValue(stage.id.ToString(),out a) || a==null || a.Length!=160 || a.Any(v=>v<0 || v>65535 || (v&127)>43 || (v>>7)>21))throw new InvalidDataException("Invalid Score Attack tile or enemy.");}}
        if(p.ScoreTargets!=null)foreach(var pair in p.ScoreTargets){int id;if(p.Version<13 || !int.TryParse(pair.Key,out id) || !ScoreInfo.Any(t=>t.id==id) || pair.Value<100 || pair.Value>999900 || pair.Value%100!=0)throw new InvalidDataException("Score targets require 100–999,900 points in steps of 100.");}
        if(p.PocketRules!=null)foreach(var pair in p.PocketRules){int id;var a=pair.Value;if(p.Version<11 || !int.TryParse(pair.Key,out id) || !PocketInfo.Any(t=>t.id==id) || a==null || a.Length!=2 || a[0]<1 || a[0]>99 || a[1]<100 || a[1]>999900 || a[1]%100!=0)throw new InvalidDataException("Pocket rules require 1–99 seconds and 100–999,900 points in steps of 100.");}
        if(p.Version>=10 && (p.PocketStages==null || p.PocketStages.Count!=20))throw new InvalidDataException("Pocket and Cushion requires all 20 stages.");
        if(p.PocketStages!=null){if(p.PocketStages.Count!=20)throw new InvalidDataException("Invalid Pocket and Cushion stage set.");foreach(var stage in PocketInfo){int[] a;if(!p.PocketStages.TryGetValue(stage.id.ToString(),out a) || a==null || a.Length!=160 || a.Any(v=>v<0 || v>65535 || !PocketTerrain.Contains(v&127) || ((v>>7)!=0 && (v>>7)!=2)) || a.Count(v=>(v>>7)==2)>8)throw new InvalidDataException("Pocket and Cushion supports native terrain and at most eight balls per stage.");}}
        foreach(var values in new[]{p.Wind,p.Music,p.Gobbler})if(values!=null)foreach(var pair in values) {
            int id;if(!int.TryParse(pair.Key,out id) || !(CombatStages.Any(t=>t.id==id && (!t.ScoreAttack || p.Version>=13)) || (p.Version>=12 && object.ReferenceEquals(values,p.Music) && PocketInfo.Any(t=>t.id==id))) || pair.Value<0 || pair.Value>(object.ReferenceEquals(values,p.Music)?81:1))throw new InvalidDataException("Invalid stage setting.");
        }
        if(p.WindStrength!=null)foreach(var pair in p.WindStrength){int id;if(p.Version<8 || !int.TryParse(pair.Key,out id) || !CombatStages.Any(t=>t.id==id && (!t.ScoreAttack || p.Version>=13)) || !Strengths.Contains(pair.Value))throw new InvalidDataException("Invalid wind strength.");}
        if(p.Timers!=null)foreach(var pair in p.Timers) {
            int id;if(!int.TryParse(pair.Key,out id) || !CombatStages.Any(t=>t.id==id && (!t.ScoreAttack || p.Version>=13)) || pair.Value==null || pair.Value.Length!=4 || pair.Value.Any(v=>v!=-1 && (v<25 || v>15000)))throw new InvalidDataException("Hazard countdowns must be between 1 and 600 seconds.");
        }
        if(p.Backdrops!=null)foreach(var pair in p.Backdrops){int id;if(p.Version<7 || !int.TryParse(pair.Key,out id) || !AllStages.Any(t=>t.id==id && (!t.Pocket || p.Version>=12) && (!t.ScoreAttack || p.Version>=13)) || pair.Value==null || pair.Value.Length!=2 || pair.Value.Any(v=>v!=-1 && v!=-2 && !AllStages.Any(t=>t.id==v && (!t.Pocket || p.Version>=12) && (!t.ScoreAttack || p.Version>=13))))throw new InvalidDataException("Invalid background selection.");}
        if(p.Hidden!=null)foreach(var pair in p.Hidden){int id;if(p.Version<9 || !int.TryParse(pair.Key,out id) || !CombatStages.Any(t=>t.id==id && (!t.ScoreAttack || p.Version>=13)) || pair.Value==null || pair.Value.Length!=160 || pair.Value.Any(v=>v!=-1 && (v<20 || v>25)))throw new InvalidDataException("Invalid hidden powerup map.");for(int i=0;i<160;i++)if(pair.Value[i]>=0 && ((id>=71?p.ScoreStages:p.Stages)[pair.Key][i]&127)!=1)throw new InvalidDataException("Hidden powerups require Breakable tiles.");}
        if(p.Electric!=null)foreach(var pair in p.Electric){int id;if(p.Version<7 || !int.TryParse(pair.Key,out id) || !CombatStages.Any(t=>t.id==id && (!t.ScoreAttack || p.Version>=13)) || pair.Value==null || pair.Value.Length!=160 || pair.Value.Any(v=>v< -1 || v>1))throw new InvalidDataException("Invalid enemy electricity setting.");}
        if(p.RegularStages!=null && (p.Version<3 || p.RegularStages.Distinct().Count()!=p.RegularStages.Length || p.RegularStages.Any(id=>!Info.Any(s=>s.id==id && s.mode!=0))))

            throw new InvalidDataException("Invalid regular-stage conversion.");

    }

    internal byte[] Export() {

        Validate(Data);

        foreach(var s in CombatStages) {

            var a=Cells(s.id);

            var spawners=a.Where(IsSpawner).Select(v=>v&127).OrderBy(v=>v).ToArray();
            if(spawners.Length>4 || !spawners.SequenceEqual(Enumerable.Range(3,spawners.Length)))
                throw new InvalidDataException(s+": invalid Pachin Spawners. Keep at most four; repaint a spawner using the merged Pachin Spawner option to repair old slot IDs.");

            if(a.Count(v=>EnemyPatch.Kind(v>>7)==1)>16 || LargeSlots(a)>8)

                throw new InvalidDataException(s+": too many enemies. Maximum: 16 small slots + 8 large slots. Varieties marked '2 slots' use two large slots each.");

            if(s.mode!=0 && !Regular(s.id) && a.Any(v=>!s.objects.Contains(v>>7)))

                throw new InvalidDataException(s+": this bonus mode does not support those enemy types. Its native clear rules and actor initialization differ from regular stages.");

            if(s.mode==2 && !Regular(s.id) && (a.Count(v=>(v>>7)==2)!=4 || a.Count(v=>(v>>7)==3)!=4))

                throw new InvalidDataException(s+": Plasma initialization requires exactly four type-2 and four type-3 balls. Keep four of each; you may reposition them.");

            if(a.Any(v=>(v>>7)>0 && (v>>7)<6 && !RenderInfo(s).enemyPoses.ContainsKey((v>>7).ToString())))

                throw new InvalidDataException(s+": this stage's graphics bank does not support one of the placed enemy types. Remove that type; importing its graphics is not implemented.");

        }

        foreach(var stage in CombatStages)if(RenderInfo(stage).mode!=0 && Enumerable.Range(0,160).Any(i=>HiddenChoice(stage.id,i)>=0))throw new InvalidDataException(stage+": enable Regular Enemies / Clear Rules to export hidden powerups, or remove them first.");
        var b=(byte[])Rom.Clone();

        foreach(var s in CombatStages) for(int i=0;i<160;i++) {

            int cell=Cells(s.id)[i],v=(cell&127)|(EnemyPatch.Kind(cell>>7)<<7),p=Offset(s.id)+2*i; b[p]=(byte)v; b[p+1]=(byte)(v>>8);

        }

        foreach(var stage in Info)if(Setting(Data.WindStrength,stage.id)>=0){int at=Offset(stage.id)-2;b[at]=(byte)Strength(stage.id);b[at+1]=0;}
        foreach(var stage in PocketInfo)for(int i=0;i<160;i++){int at=Offset(stage.id)+i*2,v=Cells(stage.id)[i];b[at]=(byte)v;b[at+1]=(byte)(v>>8);}
        foreach(var stage in PocketInfo)for(int index=0;index<2;index++){int at=Offset(stage.id)+(index==0?-6:-2),v=PocketRule(stage.id,index)/(index==0?1:100);b[at]=(byte)v;b[at+1]=(byte)(v>>8);}
        foreach(var stage in ScoreInfo){int at=Offset(stage.id)-2,v=ScoreTarget(stage.id)/100;b[at]=(byte)v;b[at+1]=(byte)(v>>8);}
        if(Data.DebugRestoration)b=BundledPatch.Apply(this,b,"bound-debug.ips");
        return StageHudPatch.Apply(this,BossRushPatch.Apply(this,EnemyPatch.Apply(this,SramPatch.Apply(this,b))));

    }

    internal static bool IsSpawner(int value){int tile=value&127;return tile>=3 && tile<=6;}
    internal static void NumberSpawners(int[] cells){int next=3;for(int i=0;i<cells.Length;i++)if(IsSpawner(cells[i]))cells[i]=Paint(cells[i],false,next++);}

    internal static int Paint(int value, bool enemies, int brush) { return enemies ? (value&127)|(brush<<7) : (value&~127)|brush; }

    internal static int LargeSlots(int[] cells) { return cells.Sum(v=>EnemyPatch.Kind(v>>7)>=4?2:EnemyPatch.Kind(v>>7)>=2?1:0); }

    internal void Write(string path, byte[] bytes, bool project) {Write(path,bytes,project?".bhproj":".vb");}
    internal void Write(string path, byte[] bytes, string required) {

        string full=Path.GetFullPath(path);

        if(string.Equals(full,Source,StringComparison.OrdinalIgnoreCase) ||

           string.Equals(Path.GetFileName(full),"BH.Debug.vb",StringComparison.OrdinalIgnoreCase) ||
           string.Equals(Path.GetFileName(full),"Bound High (World) (Proto 2).vb",StringComparison.OrdinalIgnoreCase))

            throw new IOException("Choose a new filename. The supplied ROM is protected.");

        if(!string.Equals(Path.GetExtension(full),required,StringComparison.OrdinalIgnoreCase))

            throw new IOException("Use the "+required+" extension.");

        // Write beside the destination, then replace atomically; a failed save preserves the previous file.

        string temp=full+"."+Guid.NewGuid().ToString("N")+".tmp";

        try { File.WriteAllBytes(temp,bytes); if(File.Exists(full)) File.Replace(temp,full,null); else File.Move(temp,full); }

        finally { if(File.Exists(temp)) File.Delete(temp); }

    }

}



sealed class PocketTiles {public int[] lookup {get;set;} public int[][] variants {get;set;}}
sealed class Artwork : IDisposable {
    static readonly PocketTiles pocketTiles=LoadPocketTiles();
    static PocketTiles LoadPocketTiles(){using(var r=new StreamReader(typeof(Document).Assembly.GetManifestResourceStream("pocket-autotiles.json")))return Document.Json.Deserialize<PocketTiles>(r.ReadToEnd());}
    internal Bitmap TerrainAt(StageInfo stage,int cell){
        // Native slot stages overwrite these cells after drawing ordinary terrain.
        int[] descriptors;
        if(stage.scriptedTiles!=null && stage.scriptedTiles.TryGetValue(cell.ToString(),out descriptors)){
            string key=stage.id+":scripted:"+cell;Bitmap overlay;
            if(!images.TryGetValue(key,out overlay)){
                overlay=new Bitmap(32,32);
                for(int i=0;i<16;i++)Glyph(overlay,stage,descriptors[i],i%4*8,i/4*8,false,true);
                images[key]=overlay;
            }
            return overlay;
        }
        int tile=doc.Cells(stage.id)[cell]&127,variant=-1;
        if(stage.Pocket && tile==29){int key=0,mul=1;for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++){if(dx==0 && dy==0)continue;int x=cell%16+dx,y=cell/16+dy,t=x<0 || x>=16 || y<0 || y>=10?0:doc.Cells(stage.id)[y*16+x]&127;key+=(t==0?0:t==29?1:2)*mul;mul*=3;}variant=pocketTiles.lookup[key];}
        return Tile(tile>=40?Document.Info.Single(s=>s.id==7):stage,tile,false,variant);
    }


    internal static readonly Color[] Red = { Color.Black, Color.FromArgb(105,0,0), Color.FromArgb(162,0,0), Color.FromArgb(250,0,0) };

    readonly Dictionary<string,Bitmap> images=new Dictionary<string,Bitmap>();

    readonly Document doc;

    internal Artwork(Document d) { doc=d; }

    internal Bitmap Tile(StageInfo s,int type,bool enemy,int variant=-1) {

        s=doc.RenderInfo(s);if(!enemy)s=EnemyPatch.TerrainDonor(doc,s,type);if(enemy && type>=6){s=EnemyPatch.Donor(type);type=EnemyPatch.Kind(type);}

        string key=s.id+":"+s.mode+":"+type+":"+enemy+":"+variant;

        Bitmap result;

        if(images.TryGetValue(key,out result)) return result;

        result=new Bitmap(32,32);

        if(!enemy) {

            int[] ds=variant>=0?pocketTiles.variants[variant]:s.tiles[type.ToString()];

            for(int i=0;i<16;i++) Glyph(result,s,ds[i],i%4*8,i/4*8,false,true);

        } else if(type!=0 && s.enemyPoses.ContainsKey(type.ToString())) {

            var pose=s.enemyPoses[type.ToString()];result.Dispose();result=new Bitmap(pose.width,pose.height);

            foreach(var part in pose.parts)Glyph(result,s,part[0],part[1],part[2],part[3]!=0,true);

            // Trim transparent margins only; preserve every pixel of the complete composite.

            int left=result.Width,top=result.Height,right=-1,bottom=-1;

            for(int y=0;y<result.Height;y++)for(int x=0;x<result.Width;x++)if(result.GetPixel(x,y).A!=0){left=Math.Min(left,x);top=Math.Min(top,y);right=Math.Max(right,x);bottom=Math.Max(bottom,y);}

            if(right>=0){var cropped=result.Clone(new Rectangle(left,top,right-left+1,bottom-top+1),PixelFormat.Format32bppArgb);result.Dispose();result=cropped;}

        }

        images[key]=result; return result;

    }

    void Glyph(Bitmap dest,StageInfo s,int d,int dx,int dy,bool obj,bool transparent=false) {

        int character=d&2047, address=doc.Asset(s.banks[character/1024]+(character%1024)*16);

        int palette=s.palettes[(obj?4:0)+(d>>14)];

        for(int y=0;y<8;y++) for(int x=0;x<8;x++) {

            int sx=(d&8192)!=0?7-x:x, sy=(d&4096)!=0?7-y:y;

            int bits=(BitConverter.ToUInt16(doc.Rom,address+sy*2)>>(sx*2))&3;

            if(transparent && bits==0) continue;

            if(dx+x>=0 && dy+y>=0 && dx+x<dest.Width && dy+y<dest.Height)dest.SetPixel(dx+x,dy+y,Red[(palette>>(bits*2))&3]);

        }

    }

    // Compare decoded native pixels, including transparency, not bank/descriptor IDs.
    internal string NativeBackgroundKey(StageInfo s,int plane){
        var pixels=new byte[512*512];byte[] data=Convert.FromBase64String(s.backgrounds[plane]);
        for(int i=0;i<4096;i++){
            int d=BitConverter.ToUInt16(data,i*2),chr=d&2047,at=doc.Asset(s.banks[chr/1024]+chr%1024*16),palette=s.palettes[d>>14];
            for(int y=0;y<8;y++)for(int x=0;x<8;x++){
                int sx=(d&8192)!=0?7-x:x,sy=(d&4096)!=0?7-y:y,bits=(BitConverter.ToUInt16(doc.Rom,at+sy*2)>>(sx*2))&3;
                pixels[(i/64*8+y)*512+i%64*8+x]=(byte)(bits==0?0:1+((palette>>(bits*2))&3));
            }
        }
        return Document.Digest(pixels);
    }

    internal Bitmap Background(StageInfo s,int plane=-1) {

        int choice=plane<0?-1:doc.BackgroundChoice(s.id,plane);
        string choices=doc.BackgroundChoice(s.id,0)+":"+doc.BackgroundChoice(s.id,1);
        if(choice>=0)s=Document.AllStages.Single(t=>t.id==choice);else s=doc.RenderInfo(s);
        string key=s.id+":"+s.mode+":background:"+plane+":"+choices;Bitmap b;

        if(images.TryGetValue(key,out b))return b;

        b=new Bitmap(512,512);

        if(plane<0) {

            using(var g=Graphics.FromImage(b)){g.Clear(Color.Black);for(int p=0;p<s.backgrounds.Length;p++)g.DrawImageUnscaled(Background(s,p),0,0);}

        } else if(choice!=-2) {

            byte[] data=Convert.FromBase64String(s.backgrounds[plane]);

            for(int i=0;i<4096;i++)Glyph(b,s,BitConverter.ToUInt16(data,i*2),i%64*8,i/64*8,false,true);

        }

        images[key]=b;return b;

    }

    internal Bitmap Map(StageInfo s,bool background=true) {

        var b=new Bitmap(512,320);

        using(var g=Graphics.FromImage(b)) {

            g.Clear(Color.Black);

            if(background)g.DrawImageUnscaled(Background(s),0,0);

            for(int i=0;i<160;i++) {

                int v=doc.Cells(s.id)[i]; g.DrawImageUnscaled(TerrainAt(s,i),i%16*32,i/16*32);

            }

            for(int i=0;i<160;i++) {

                int enemy=doc.Cells(s.id)[i]>>7,size=EnemyPatch.Kind(enemy)>=4?64:32;

                if(enemy!=0)DrawEnemy(g,s,enemy,new Rectangle(i%16*32,i/16*32,size,size));

            }

        }

        return b;

    }

    internal void DrawEnemy(Graphics g,StageInfo s,int type,Rectangle r) {

        var b=Tile(s,type,true);double scale=Math.Min((double)r.Width/b.Width,(double)r.Height/b.Height);

        int w=Math.Max(1,(int)(b.Width*scale)),h=Math.Max(1,(int)(b.Height*scale));

        var state=g.Save();g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;

        g.DrawImage(b,new Rectangle(r.X+(r.Width-w)/2,r.Y+(r.Height-h)/2,w,h));g.Restore(state);

    }

    internal void Rip(string dir) {

        Directory.CreateDirectory(dir);

        string global=Path.Combine(dir,"adventure-enemies");Directory.CreateDirectory(global);

        for(int code=6;code<22;code++)Tile(Document.Info[0],code,true).Save(Path.Combine(global,EnemyPatch.Names[code-6]+".png"),ImageFormat.Png);

        foreach(var s in Document.AllStages) {

            string sub=Path.Combine(dir,s.ScoreAttack?"score-attack":s.Pocket?"pocket-and-cushion":"world-"+s.World,"stage-"+(s.ScoreAttack?s.id-70:s.Pocket?s.id-49:s.Number).ToString("00"));Directory.CreateDirectory(sub);

            foreach(int t in Enumerable.Range(0,44))Tile(s,t,false).Save(Path.Combine(sub,"terrain-"+t+".png"),ImageFormat.Png);

            foreach(string t in s.enemyPoses.Keys)Tile(s,int.Parse(t),true).Save(Path.Combine(sub,"enemy-"+t+".png"),ImageFormat.Png);

            Background(s).Save(Path.Combine(sub,"background.png"),ImageFormat.Png);

            for(int p=0;p<s.backgrounds.Length;p++)Background(s,p).Save(Path.Combine(sub,"background-layer-"+p+".png"),ImageFormat.Png);

            using(var map=Map(s))map.Save(Path.Combine(sub,"stage.png"),ImageFormat.Png);

        }

        File.WriteAllText(Path.Combine(dir,"palette.txt"),"Black #000000\r\nRed 1 #690000\r\nRed 2 #A20000\r\nRed 3 #FA0000\r\nBound High brightness registers: 32,64,32; column repeat: 0.\r\nRGB mapping matches Lemur's emulator core, not a calibrated physical LED display.\r\nTerrain: 32x32 pixels; enemy poses: complete native-size transparent composites; stage previews: 512x320.\r\nROM SHA-1: "+doc.SourceHash+"\r\n");

    }

    public void Dispose() { foreach(var b in images.Values)b.Dispose(); }

}



sealed class MapCanvas : Control {

    internal Editor Host;

    internal int Cell=48, Selected;

    internal bool Grid=true, ShowBackground=true, ShowBackground2=true, ShowTerrain=true, ShowEnemies=true;

    bool dragging;

    internal MapCanvas() { DoubleBuffered=true; TabStop=true; AccessibleName="Stage grid"; ResizeGrid(); }

    internal void ResizeGrid() { Size=new Size(Cell*16+1,Cell*10+1); Invalidate(); }

    protected override void OnPaint(PaintEventArgs e) {

        e.Graphics.Clear(Color.Black);

        if(Host==null || Host.Doc==null || Host.Stage==null) return;

        e.Graphics.InterpolationMode=InterpolationMode.NearestNeighbor; e.Graphics.PixelOffsetMode=PixelOffsetMode.Half;

        if(ShowBackground)e.Graphics.DrawImage(Host.Art.Background(Host.Stage,0),new Rectangle(0,0,Cell*16,Cell*16));

        if(ShowBackground2)e.Graphics.DrawImage(Host.Art.Background(Host.Stage,1),new Rectangle(0,0,Cell*16,Cell*16));

        if(ShowTerrain)for(int i=0;i<160;i++) {
            int tile=Host.Doc.Cells(Host.Stage.id)[i]&127;
            e.Graphics.DrawImage(Host.Art.TerrainAt(Host.Stage,i),new Rectangle(i%16*Cell,i/16*Cell,Cell,Cell));
            int hidden=Host.Doc.HiddenChoice(Host.Stage.id,i);if(hidden>=0){e.Graphics.DrawImage(Host.Art.Tile(Host.Stage,hidden,false),new Rectangle(i%16*Cell+Cell/2,i/16*Cell+Cell/2,Cell/2,Cell/2));Badge(e.Graphics,"H",i,false);}
            if(tile==17)Badge(e.Graphics,"M",i,false);
        }

        if(ShowEnemies)for(int i=0;i<160;i++) {

            int enemy=Host.Doc.Cells(Host.Stage.id)[i]>>7,size=EnemyPatch.Kind(enemy)>=4?Cell*2:Cell;

            if(enemy!=0)Host.Art.DrawEnemy(e.Graphics,Host.Stage,enemy,new Rectangle(i%16*Cell,i/16*Cell,size,size));
            if(Host.Doc.ElectricOn(Host.Stage,i))Badge(e.Graphics,"E",i,true);

        }

        if(ShowTerrain)for(int i=0;i<160;i++)if(Host.Doc.HiddenChoice(Host.Stage.id,i)>=0)Badge(e.Graphics,"H",i,false);
        if(Grid)for(int i=0;i<160;i++)e.Graphics.DrawRectangle(Pens.DimGray,new Rectangle(i%16*Cell,i/16*Cell,Cell,Cell));

        if(Focused) e.Graphics.DrawRectangle(Pens.White,Selected%16*Cell+1,Selected/16*Cell+1,Cell-2,Cell-2);

    }

    void Badge(Graphics g,string text,int cell,bool right){TextRenderer.DrawText(g,text,Font,new Rectangle(cell%16*Cell+(right?Cell-18:1),cell/16*Cell+1,17,18),Color.White,Color.FromArgb(45,45,45),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);}

    void At(MouseEventArgs e) {

        int x=e.X/Cell,y=e.Y/Cell;

        if(e.X<0 || e.Y<0 || x>=16 || y>=10 || Host.Doc==null) return;

        Selected=y*16+x;

        if(dragging) Host.PaintCell(Selected,e.Button==MouseButtons.Right);

        Host.Hover(Selected); Invalidate();

    }

    protected override void OnMouseDown(MouseEventArgs e) { Focus(); if(e.Button==MouseButtons.Left || e.Button==MouseButtons.Right) { Host.BeginStroke(); dragging=true; Capture=true; At(e); } }

    protected override void OnMouseMove(MouseEventArgs e) { At(e); }

    protected override void OnMouseUp(MouseEventArgs e) { dragging=false; Capture=false; Host.EndStroke(); }

    protected override void OnMouseCaptureChanged(EventArgs e) { if(dragging) {dragging=false;Host.EndStroke();} base.OnMouseCaptureChanged(e); }

    protected override bool IsInputKey(Keys k) { return k==Keys.Left || k==Keys.Right || k==Keys.Up || k==Keys.Down || base.IsInputKey(k); }

    protected override void OnKeyDown(KeyEventArgs e) {

        int x=Selected%16,y=Selected/16;

        if(e.KeyCode==Keys.Left)x=Math.Max(0,x-1); if(e.KeyCode==Keys.Right)x=Math.Min(15,x+1);

        if(e.KeyCode==Keys.Up)y=Math.Max(0,y-1); if(e.KeyCode==Keys.Down)y=Math.Min(9,y+1);

        Selected=y*16+x;

        if(e.KeyCode==Keys.Space || e.KeyCode==Keys.Delete) { Host.BeginStroke();Host.PaintCell(Selected,e.KeyCode==Keys.Delete);Host.EndStroke();e.Handled=true; }

        Host.Hover(Selected);Invalidate();base.OnKeyDown(e);

    }

}



sealed class Editor : Form {

    internal Document Doc;

    internal Artwork Art;

    internal StageInfo Stage {get {return stages.SelectedItem as StageInfo;}}

    readonly ListBox stages=new ListBox(), palette=new ListBox();

    readonly ComboBox layer=new ComboBox(), zoom=new ComboBox();

    readonly CheckBox eraser=new ThemeCheckBox {Text="⌫  Eraser",Appearance=Appearance.Button,AutoSize=true,AccessibleName="Eraser for selected layer"};

    internal readonly ToolStripMenuItem dark=new ToolStripMenuItem("Dark Mode") {Checked=true,CheckOnClick=true};

    readonly CheckBox electricBrush=new ThemeCheckBox {Text="Electric Variant",AccessibleName="Electric Enemy Variant"};
    readonly ComboBox[] backdrops=new ComboBox[2];
    readonly Dictionary<int,int>[] backgroundAliases={new Dictionary<int,int>(),new Dictionary<int,int>()};
    readonly CheckBox regular=new ThemeCheckBox {Text="Regular Enemies / Clear Rules",AutoSize=true};

    readonly ComboBox windStrength=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Width=180,AccessibleName="Wind Strength"};
    readonly CheckBox wind=new HazardCheckBox {Text="Turn Wind Hazard on?",AccessibleName="Wind Hazard"};
    readonly CheckBox gobbler=new HazardCheckBox {Text="Turn Gobbler Hazard on?",AccessibleName="Gobbler Hazard"};
    readonly CheckBox worldMusic=new ThemeCheckBox {Text="Apply BGM to World?",AutoSize=true,AccessibleName="Apply BGM to World"};
    readonly CheckBox[] gameTiming=new CheckBox[2];
    readonly NumericUpDown[] countdown=new NumericUpDown[4];
    readonly Label[] timingNotes=new Label[2];
    readonly ComboBox music=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Width=240,AccessibleName="Stage BGM"};
    internal readonly ToolStripMenuItem debugRestoration=new ToolStripMenuItem("Debug Restoration") {Enabled=false,Tag="RightCheck"};
    internal readonly ToolStripMenuItem sram=new ToolStripMenuItem("SRAM Support") {Enabled=false,Tag="RightCheck"};
    internal readonly ToolStripMenuItem stageHud=new ToolStripMenuItem("Stage Number in HUD") {Enabled=false,Tag="RightCheck"};
    internal readonly ToolStripMenuItem bossRush=new ToolStripMenuItem("Add Boss Rush Mode") {Enabled=false,Tag="RightCheck"};
    readonly ToolStripMenuItem paintTool=new ToolStripMenuItem("Paint"),eraseTool=new ToolStripMenuItem("Eraser");
    readonly ToolTip audioTips=new ToolTip();
    System.Threading.CancellationTokenSource audioCancel;
    bool changingStage,pocketMode,scoreMode;
    readonly ToolStripMenuItem adventureMode=new ToolStripMenuItem("Adventure of Chalvo"),pocketMenu=new ToolStripMenuItem("Pocket and Cushion"),scoreMenu=new ToolStripMenuItem("Score Attack!!");
    readonly NumericUpDown scoreTarget=new NumericUpDown {Minimum=100,Maximum=999900,Increment=100,Value=100,ThousandsSeparator=true,Width=230,AccessibleName="Score Attack Score Target"};
    readonly Label scoreDefault=new Label {AutoSize=true};
    FlowLayoutPanel scoreSettings;
    FlowLayoutPanel stageSettings,pocketSettings;Control[] mediaControls;TableLayoutPanel paletteLayout;TableLayoutPanel mapLayout;
    readonly NumericUpDown pocketTime=new NumericUpDown {Minimum=1,Maximum=99,Value=99,Width=230,AccessibleName="Pocket Time Limit"};
    readonly NumericUpDown pocketScore=new NumericUpDown {Minimum=100,Maximum=999900,Increment=100,Value=100,ThousandsSeparator=true,Width=230,AccessibleName="Pocket Score Target"};
    readonly Label pocketTimeDefault=new Label {AutoSize=true},pocketScoreDefault=new Label {AutoSize=true};
    CheckBox terrainToggle,enemyToggle;

    internal readonly MapCanvas canvas=new MapCanvas();

    readonly ComboBox worlds=new ComboBox();

    readonly Label heading=new Label(), rules=new Label();

    readonly ToolStripStatusLabel status=new ToolStripStatusLabel();

    readonly Stack<string> undo=new Stack<string>(),redo=new Stack<string>();

    string saved="",projectPath="",stroke;

    bool Dirty {get {return Doc!=null && Doc.Serialize()!=saved;}}

    internal Editor(string initial) {
        Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        Text="BH! Editor v1.0"; Width=Math.Min(1480,Screen.PrimaryScreen.WorkingArea.Width); Height=820; MinimumSize=new Size(980,680);

        StartPosition=FormStartPosition.CenterScreen; Font=new Font("Segoe UI",10); AutoScaleMode=AutoScaleMode.Dpi;

        BackColor=Color.FromArgb(242,242,242); KeyPreview=true;

        var toolbar=new MenuStrip {Padding=new Padding(8)};MainMenuStrip=toolbar;
        var file=new ToolStripMenuItem("File");var rom=new ToolStripMenuItem("ROM");var tools=new ToolStripMenuItem("Tools");
        var modes=new ToolStripMenuItem("Game Mode");toolbar.Items.AddRange(new ToolStripItem[]{file,rom,tools,modes,new ToolStripMenuItem("Enhancements",null,new ToolStripItem[]{bossRush,stageHud,sram,debugRestoration})});modes.DropDownItems.AddRange(new ToolStripItem[]{adventureMode,scoreMenu,pocketMenu});adventureMode.Checked=true;
        debugRestoration.Click+=(s,e)=>{if(Doc==null || !Doc.IsProto2)return;BeginStroke();Doc.Data.DebugRestoration=!Doc.Data.DebugRestoration;EndStroke();};
        sram.Click+=(s,e)=>{if(Doc==null || !Doc.IsProto2)return;BeginStroke();Doc.Data.Sram=!Doc.Data.Sram;EndStroke();};
        stageHud.Click+=(s,e)=>{if(Doc==null)return;BeginStroke();Doc.Data.StageHud=!Doc.Data.StageHud;EndStroke();};
        bossRush.Click+=(s,e)=>{if(Doc==null)return;BeginStroke();Doc.Data.BossRush=!Doc.Data.BossRush;EndStroke();};
        adventureMode.Click+=(s,e)=>SwitchMode(0);scoreMenu.Click+=(s,e)=>SwitchMode(1);pocketMenu.Click+=(s,e)=>SwitchMode(2);
        AddMenu(file,"New Project",()=>{if(Doc==null)OpenRom();else if(CanDiscard())LoadRom(Doc.Source);});
        AddMenu(file,"Open Project",()=>OpenProject());AddMenu(file,"Save Project",()=>SaveProject(false),Keys.Control|Keys.S);
        AddMenu(file,"Save Project As",()=>SaveProject(true));AddMenu(file,"Exit",()=>Close());
        AddMenu(rom,"Load ROM",()=>OpenRom());AddMenu(rom,"Export ROM",()=>Export());
        AddMenu(rom,"Dump Game Assets",()=>Rip());AddMenu(rom,"Dump Audio Assets",()=>Audio(true,0));
        AddMenu(tools,"Undo",()=>History(undo,redo),Keys.Control|Keys.Z);AddMenu(tools,"Redo",()=>History(redo,undo),Keys.Control|Keys.Y);
        tools.DropDownItems.AddRange(new ToolStripItem[]{paintTool,eraseTool,dark});paintTool.Checked=true;
        foreach(var item in new[]{paintTool,eraseTool,dark}){item.Tag="RightCheck";}
        paintTool.Click+=(s,e)=>eraser.Checked=false;eraseTool.Click+=(s,e)=>eraser.Checked=true;
        eraser.CheckedChanged+=(s,e)=>{paintTool.Checked=!eraser.Checked;eraseTool.Checked=eraser.Checked;};

        var body=new SplitContainer {Dock=DockStyle.Fill,FixedPanel=FixedPanel.Panel1,SplitterDistance=235};

        var left=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=5,Padding=new Padding(12)};

        paletteLayout=left;
        left.RowStyles.Add(new RowStyle(SizeType.Absolute,30));left.RowStyles.Add(new RowStyle(SizeType.Absolute,250));

        left.RowStyles.Add(new RowStyle(SizeType.Absolute,35));

        left.RowStyles.Add(new RowStyle(SizeType.Percent,100));left.RowStyles.Add(new RowStyle(SizeType.Absolute,36));

        worlds.DropDownStyle=ComboBoxStyle.DropDownList;worlds.Items.AddRange(new object[]{"World 1","World 2","World 3","World 4"});worlds.SelectedIndex=0;worlds.Dock=DockStyle.Fill;worlds.AccessibleName="Adventure world";ConfigureCombo(worlds);left.Controls.Add(worlds,0,0);

        stages.Dock=DockStyle.Fill;stages.IntegralHeight=false;stages.AccessibleName="Adventure stages";left.Controls.Add(stages,0,1);

        layer.DropDownStyle=ComboBoxStyle.DropDownList;layer.Items.AddRange(new object[]{"Terrain","Enemies","Powerups","Hidden Powerups"});layer.Dock=DockStyle.Fill;

        layer.AccessibleName="Editing layer";layer.SelectedIndex=0;left.Controls.Add(layer,0,2);ConfigureCombo(layer);


        palette.Dock=DockStyle.Fill;palette.DrawMode=DrawMode.OwnerDrawFixed;palette.ItemHeight=52;palette.IntegralHeight=false;

        palette.AccessibleName="Tile palette";left.Controls.Add(palette,0,3);left.Controls.Add(electricBrush,0,4);electricBrush.Visible=false;body.Panel1.Controls.Add(left);
        audioTips.SetToolTip(electricBrush,"Check to paint electric enemies; uncheck for ordinary contact. Repaint an existing enemy to change it. Movement is preserved.");

        var right=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Padding=new Padding(12)};

        right.RowStyles.Add(new RowStyle(SizeType.Absolute,40));right.RowStyles.Add(new RowStyle(SizeType.Absolute,40));

        right.RowStyles.Add(new RowStyle(SizeType.Percent,100));right.RowStyles.Add(new RowStyle(SizeType.Absolute,94));

        heading.Text="Load a Bound High! ROM to begin";heading.Dock=DockStyle.Fill;heading.Font=new Font("Segoe UI",16,FontStyle.Bold);right.Controls.Add(heading,0,0);

        var options=new FlowLayoutPanel {Dock=DockStyle.Fill};

        zoom.DropDownStyle=ComboBoxStyle.DropDownList;zoom.Items.AddRange(new object[]{"100%","150%","200%"});zoom.SelectedIndex=1;zoom.Width=85;zoom.AccessibleName="Zoom";ConfigureCombo(zoom);

        var grid=new ThemeCheckBox {Text="Grid",Checked=true,AutoSize=true};

        var background=new ThemeCheckBox {Text="BG Layer 01",Checked=true,AutoSize=true};

        var background2=new ThemeCheckBox {Text="BG Layer 02",Checked=true,AutoSize=true};

        var terrainView=new ThemeCheckBox {Text="Terrain/Powerups",Checked=true,AutoSize=true};

        var enemyView=new ThemeCheckBox {Text="Enemies",Checked=true,AutoSize=true};

        terrainToggle=terrainView;enemyToggle=enemyView;
        options.Controls.Add(new Label {Text="Zoom",AutoSize=true,Margin=new Padding(0,6,8,0)});options.Controls.Add(zoom);options.Controls.Add(grid);options.Controls.Add(background);options.Controls.Add(background2);options.Controls.Add(terrainView);options.Controls.Add(enemyView);right.Controls.Add(options,0,1);

        var scroll=new Panel {Dock=DockStyle.Fill,AutoScroll=true,BackColor=Color.FromArgb(22,22,22),Padding=new Padding(8)};

        canvas.Host=this;canvas.Location=new Point(8,8);scroll.Controls.Add(canvas);
        var mapArea=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};
        mapArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));mapArea.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,290));
        mapArea.Controls.Add(scroll,0,0);
        var settings=new FlowLayoutPanel {Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,AutoScrollMargin=new Size(0,24),Padding=new Padding(10)};
        stageSettings=settings;mapLayout=mapArea;
        scoreSettings=new FlowLayoutPanel {AutoSize=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Visible=false,Margin=new Padding(0,0,0,12)};
        scoreSettings.Controls.Add(new Label {Text="Score Target (Points)",AutoSize=true});scoreSettings.Controls.Add(scoreTarget);scoreSettings.Controls.Add(scoreDefault);
        var resetScore=new Button {Text="Restore Default Score Target",AutoSize=true};scoreSettings.Controls.Add(resetScore);settings.Controls.Add(scoreSettings);
        resetScore.Click+=(s,e)=>{if(Stage==null || !Stage.ScoreAttack)return;BeginStroke();Doc.SetScoreTarget(Stage.id,Doc.ScoreDefault(Stage.id));EndStroke();UpdateRules();};
        scoreTarget.ValueChanged+=(s,e)=>{if(changingStage || Stage==null || !Stage.ScoreAttack)return;BeginStroke();Doc.SetScoreTarget(Stage.id,(int)(Math.Round(scoreTarget.Value/100m,MidpointRounding.AwayFromZero)*100));EndStroke();};
        scoreTarget.Validated+=(s,e)=>{if(Stage!=null && Stage.ScoreAttack)UpdateRules();};
        settings.Controls.Add(wind);settings.Controls.Add(new Label {Text="Wind Strength",AutoSize=true});windStrength.Items.AddRange(new object[]{"Light","Average","Moderate","Strong","Stronger"});settings.Controls.Add(windStrength);AddTimers(settings,0);settings.Controls.Add(gobbler);AddTimers(settings,1);var mediaSettings=new FlowLayoutPanel {AutoSize=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Width=245,Margin=new Padding(0)};settings.Controls.Add(mediaSettings);mediaSettings.Controls.Add(new Label {Text="Stage BGM",AutoSize=true,Margin=new Padding(3,16,3,5)});
        music.Items.Add("Original Stage Music");music.Items.Add("Silence");
        for(int i=1;i<=81;i++)music.Items.Add(new AudioTrack(i).ToString());
        ConfigureCombo(music);mediaSettings.Controls.Add(music);
        var playback=new FlowLayoutPanel {AutoSize=true,WrapContents=false};
        AddAudioButton(playback,"|◀","Previous Track",()=>StepTrack(-1));
        AddAudioButton(playback,"▶","Play",()=>PlayStageAudio());
        AddAudioButton(playback,"■","Stop",()=>StopStageAudio());
        AddAudioButton(playback,"▶|","Next Track",()=>StepTrack(1));mediaSettings.Controls.Add(playback);
        worldMusic.CheckedChanged+=(s,e)=>{if(!changingStage && worldMusic.Checked)ApplyMusic();};
        audioTips.SetToolTip(worldMusic,"When checked, apply this selection and later BGM changes to all editable stages in this world. Resets when you switch stages.");
        audioTips.SetToolTip(gobbler,"Enables the native recurring hazard. Needs enemies, an eligible row or column, and a free large-actor slot. Uses the stage timer, or 30 seconds on Easy when none is stored.");
        mediaSettings.Controls.Add(worldMusic);
        for(int plane=0;plane<2;plane++){
            int index=plane;mediaSettings.Controls.Add(new Label {Text="BG Layer "+(plane+1).ToString("00"),AutoSize=true,Margin=new Padding(3,12,3,3)});
            var box=new ComboBox {Width=230,DropDownStyle=ComboBoxStyle.DropDownList,AccessibleName="BG Layer "+(plane+1).ToString("00")};backdrops[plane]=box;
            box.Items.Add("Original Background");box.Items.Add("None");
            ConfigureCombo(box);mediaSettings.Controls.Add(box);
            box.SelectedIndexChanged+=(s,e)=>{if(changingStage || Stage==null || box.SelectedIndex<0)return;BeginStroke();Doc.SetBackground(Stage.id,index,box.SelectedIndex==0?-1:box.SelectedIndex==1?-2:((StageInfo)box.SelectedItem).id);EndStroke();canvas.Invalidate();};
        }
        mediaControls=mediaSettings.Controls.Cast<Control>().ToArray();foreach(var control in mediaControls)settings.Controls.Add(control);settings.Controls.Remove(mediaSettings);mediaSettings.Dispose();
        regular.MaximumSize=new Size(240,0);settings.Controls.Add(regular);
        var settingsHost=new Panel {Dock=DockStyle.Fill};settingsHost.Controls.Add(settings);
        pocketSettings=new FlowLayoutPanel {Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,AutoScrollMargin=new Size(0,24),Padding=new Padding(10),Visible=false};
        pocketSettings.Controls.Add(new Label {Text="Time Limit (Seconds)",AutoSize=true});pocketSettings.Controls.Add(pocketTime);pocketSettings.Controls.Add(pocketTimeDefault);
        pocketSettings.Controls.Add(new Label {Text="Score Target (Points)",AutoSize=true,Margin=new Padding(3,8,3,3)});pocketSettings.Controls.Add(pocketScore);pocketSettings.Controls.Add(pocketScoreDefault);
        pocketSettings.Controls.Add(new Label {Text="100-point increments",AutoSize=true});
        var resetPocket=new Button {Text="Restore Stage Defaults",AutoSize=true,Margin=new Padding(3,8,3,3)};pocketSettings.Controls.Add(resetPocket);pocketSettings.Controls.Add(new Label {Text="Ball-on-Ball Collision: 200 Points",AutoSize=true,MaximumSize=new Size(240,0),Margin=new Padding(3,8,3,3)});
        resetPocket.Click+=(s,e)=>{if(Stage==null || !Stage.Pocket)return;BeginStroke();Doc.SetPocketRules(Stage.id,Doc.PocketDefault(Stage.id,0),Doc.PocketDefault(Stage.id,1));EndStroke();UpdateRules();};
        pocketTime.ValueChanged+=(s,e)=>SavePocketRules();pocketScore.ValueChanged+=(s,e)=>SavePocketRules();pocketScore.Validated+=(s,e)=>{if(Stage!=null && Stage.Pocket)UpdateRules();};
        foreach(var sidebar in new[]{settings,pocketSettings})sidebar.Layout+=(s,e)=>{
            int bottom=sidebar.Controls.Cast<Control>().Where(c=>c.Visible).Select(c=>c.Bottom+c.Margin.Bottom-sidebar.AutoScrollPosition.Y).DefaultIfEmpty(0).Max()+sidebar.Padding.Bottom+24;
            if(sidebar.AutoScrollMinSize.Height!=bottom)sidebar.AutoScrollMinSize=new Size(0,bottom);
        };
        settingsHost.Controls.Add(pocketSettings);mapArea.Controls.Add(settingsHost,1,0);right.Controls.Add(mapArea,0,2);
        windStrength.SelectedIndexChanged+=(s,e)=>{if(changingStage || Stage==null || windStrength.SelectedIndex<0)return;BeginStroke();Doc.SetStrength(Stage.id,Document.Strengths[windStrength.SelectedIndex]);Doc.SetSetting(true,Stage.id,wind.Checked?1:0);EndStroke();};
        wind.CheckedChanged+=(s,e)=>{if(changingStage || Stage==null)return;BeginStroke();Doc.SetSetting(true,Stage.id,wind.Checked?1:0);EndStroke();UpdateRules();};
        gobbler.CheckedChanged+=(s,e)=>{if(changingStage || Stage==null)return;BeginStroke();Doc.SetGobbler(Stage.id,gobbler.Checked?1:0);EndStroke();UpdateRules();};
        music.SelectedIndexChanged+=(s,e)=>{if(changingStage || Stage==null)return;StopStageAudio();ApplyMusic();};

        rules.Dock=DockStyle.Fill;rules.Padding=new Padding(0,8,0,0);right.Controls.Add(rules,0,3);

        body.Panel2.Controls.Add(right);

        var bar=new StatusStrip();bar.Items.Add(status);status.Text="Load a ROM to edit. Originals are preserved.";

        Controls.Add(body);Controls.Add(toolbar);Controls.Add(bar);toolbar.Dock=DockStyle.Top;bar.Dock=DockStyle.Bottom;

        Shown+=(s,e)=>{body.SplitterDistance=255;};

        stages.SelectedIndexChanged+=(s,e)=>ChangeStage();layer.SelectedIndexChanged+=(s,e)=>{palette.BeginUpdate();try{palette.SelectedIndex=-1;RefreshPalette();}finally{palette.EndUpdate();}};

        worlds.SelectedIndexChanged+=(s,e)=>ShowWorld();

        palette.SelectedIndexChanged+=(s,e)=>{electricBrush.Visible=!pocketMode && layer.SelectedIndex==1 && palette.SelectedItem!=null && (int)palette.SelectedItem>=6 && (int)palette.SelectedItem!=12;electricBrush.Enabled=electricBrush.Visible;if(layer.SelectedIndex==0 && palette.SelectedItem!=null && (int)palette.SelectedItem==3 && SpawnerCount>=4){palette.SelectedIndex=-1;return;}if(pocketMode && palette.SelectedItem!=null && (int)palette.SelectedItem==256 && BallCount>=8){palette.SelectedIndex=-1;return;}eraser.Checked=false;};palette.DrawItem+=DrawPalette;

        grid.CheckedChanged+=(s,e)=>{canvas.Grid=grid.Checked;canvas.Invalidate();};

        background.CheckedChanged+=(s,e)=>{canvas.ShowBackground=background.Checked;canvas.Invalidate();};

        background2.CheckedChanged+=(s,e)=>{canvas.ShowBackground2=background2.Checked;canvas.Invalidate();};

        terrainView.CheckedChanged+=(s,e)=>{canvas.ShowTerrain=terrainView.Checked;canvas.Invalidate();};

        enemyView.CheckedChanged+=(s,e)=>{canvas.ShowEnemies=enemyView.Checked;canvas.Invalidate();};

        dark.CheckedChanged+=(s,e)=>ApplyTheme();

        regular.CheckedChanged+=(s,e)=>{if(changingStage || Stage==null || Doc==null)return;StopStageAudio();BeginStroke();Doc.SetRegular(Stage.id,regular.Checked);EndStroke();RefreshPalette();UpdateRules();canvas.Invalidate();};

        zoom.SelectedIndexChanged+=(s,e)=>{canvas.Cell=32+zoom.SelectedIndex*16;canvas.ResizeGrid();};

        FormClosing+=(s,e)=>{if(!CanDiscard())e.Cancel=true;};



        if(initial!=null) LoadRom(initial);

        ApplyTheme();

    }

    internal void ApplyTheme() {

        Color back=dark.Checked?Color.FromArgb(29,31,35):SystemColors.Control;

        Color panel=dark.Checked?Color.FromArgb(39,42,47):SystemColors.Window;

        Color fore=dark.Checked?Color.FromArgb(232,232,235):SystemColors.ControlText;

        ThemeControl(this,back,panel,fore);

        palette.Invalidate();canvas.Invalidate();

    }

    void ConfigureCombo(ComboBox combo) {

        combo.DrawMode=DrawMode.OwnerDrawFixed;

        combo.DrawItem+=(s,e)=>{

            using(var b=new SolidBrush(combo.BackColor))e.Graphics.FillRectangle(b,e.Bounds);

            if(e.Index>=0)TextRenderer.DrawText(e.Graphics,combo.Items[e.Index].ToString(),Font,e.Bounds,combo.ForeColor,TextFormatFlags.Left|TextFormatFlags.VerticalCenter);

            e.DrawFocusRectangle();

        };

    }

    internal void ThemeControl(Control c,Color back,Color panel,Color fore) {

        c.BackColor=(c is ListBox || c is ComboBox)?panel:back;c.ForeColor=fore;

        var combo=c as ComboBox;if(combo!=null)combo.FlatStyle=FlatStyle.Flat;

        var check=c as CheckBox;if(check!=null){check.FlatStyle=FlatStyle.Flat;check.UseVisualStyleBackColor=false;}

        var strip=c as ToolStrip;if(strip!=null){strip.Renderer=new MenuRenderer(dark.Checked);foreach(ToolStripItem item in strip.Items){ThemeItem(item,back,fore);}}

        foreach(Control child in c.Controls)ThemeControl(child,back,panel,fore);

    }

    void AddMenu(ToolStripMenuItem menu,string text,Action action,Keys shortcut=Keys.None) {var item=new ToolStripMenuItem(text) {ShortcutKeys=shortcut};item.Click+=(s,e)=>Safe(action);menu.DropDownItems.Add(item);}
    void ThemeItem(ToolStripItem item,Color back,Color fore) {item.BackColor=back;item.ForeColor=fore;var menu=item as ToolStripDropDownItem;if(menu!=null){menu.DropDown.BackColor=back;menu.DropDown.Renderer=new MenuRenderer(dark.Checked);var popup=menu.DropDown as ToolStripDropDownMenu;if(popup!=null){popup.ShowImageMargin=false;popup.ShowCheckMargin=false;if(menu.DropDownItems.Cast<ToolStripItem>().Any(i=>(string)i.Tag=="RightCheck")){popup.MinimumSize=new Size(181,0);foreach(ToolStripItem child in menu.DropDownItems){child.AutoSize=false;child.Size=new Size(Math.Max(180,TextRenderer.MeasureText(child.Text,child.Font).Width+48),24);}}}foreach(ToolStripItem child in menu.DropDownItems)ThemeItem(child,back,fore);}}

    void Safe(Action a) {try {a();} catch(Exception ex) {MessageBox.Show(this,ex.Message,"Bound High Editor",MessageBoxButtons.OK,MessageBoxIcon.Error);}}

    bool Ready() {if(Doc!=null)return true;MessageBox.Show(this,"Load a supported original Bound High! ROM first.");return false;}

    bool CanDiscard() {

        if(!Dirty)return true;

        var r=MessageBox.Show(this,"Save your project before continuing?","Unsaved changes",MessageBoxButtons.YesNoCancel,MessageBoxIcon.Question);

        if(r==DialogResult.Cancel)return false;

        if(r==DialogResult.Yes){Safe(()=>SaveProject(false));return !Dirty;} return true;

    }

    internal void LoadRom(string path) {

        var d=new Document(path);var art=new Artwork(d);

        StopStageAudio();if(Art!=null)Art.Dispose();Doc=d;Art=art;projectPath="";saved=Doc.Serialize();undo.Clear();redo.Clear();

        PopulateBackgrounds();ShowWorld();UpdateTitle();

        status.Text="ROM verified · Adventure, Score Attack!! and Pocket and Cushion ready · Graphics decoded from your ROM";

    }

    void PopulateBackgrounds(){
        changingStage=true;
        for(int plane=0;plane<2;plane++){
            var box=backdrops[plane];box.Items.Clear();box.Items.Add("Original Background");box.Items.Add("None");backgroundAliases[plane].Clear();
            var unique=new Dictionary<string,int>();unique[Document.Digest(new byte[512*512])]=-2;
            foreach(var source in Document.AllStages){string key=Art.NativeBackgroundKey(source,plane);int representative;
                if(!unique.TryGetValue(key,out representative)){representative=source.id;unique[key]=representative;box.Items.Add(source);}
                backgroundAliases[plane][source.id]=representative;
            }
        }
        changingStage=false;
    }

    void OpenRom() {if(!CanDiscard())return;using(var f=new OpenFileDialog {Filter="Virtual Boy ROM (*.vb)|*.vb"})if(f.ShowDialog(this)==DialogResult.OK)LoadRom(f.FileName);}

    void OpenProject() {

        if(!Ready() || !CanDiscard())return;

        using(var f=new OpenFileDialog {Filter="Bound High project (*.bhproj)|*.bhproj"}) if(f.ShowDialog(this)==DialogResult.OK) {

            if(new FileInfo(f.FileName).Length>1000000)throw new InvalidDataException("Project file is too large.");

            Doc.Load(File.ReadAllText(f.FileName));StopStageAudio();worldMusic.Checked=false;projectPath=f.FileName;saved=Doc.Serialize();undo.Clear();redo.Clear();RefreshPalette();canvas.Invalidate();UpdateTitle();UpdateRules();

        }

    }

    void SaveProject(bool saveAs) {

        if(!Ready())return;string path=projectPath;

        if(saveAs || path=="") using(var f=new SaveFileDialog {Filter="Bound High project (*.bhproj)|*.bhproj",DefaultExt="bhproj",FileName=path==""?"BoundHigh.bhproj":Path.GetFileName(path)}) {

            if(f.ShowDialog(this)!=DialogResult.OK)return;path=f.FileName;

        }

        string text=Doc.Serialize();Doc.Write(path,System.Text.Encoding.UTF8.GetBytes(text),true);projectPath=path;saved=text;UpdateTitle();status.Text="Project saved · "+path;

    }

    void Export() {

        if(!Ready())return;byte[] bytes=Doc.Export();

        using(var f=new SaveFileDialog {Filter="Virtual Boy ROM (*.vb)|*.vb",DefaultExt="vb",FileName="BoundHigh-edited.vb"}) if(f.ShowDialog(this)==DialogResult.OK) {

            Doc.Write(f.FileName,bytes,false);status.Text="Patched ROM exported · "+f.FileName;

            MessageBox.Show(this,"ROM successfully patched and exported!\n\nDon't forget to also save your project file!!!","Export complete");

        }

    }

    void Rip() {

        if(!Ready())return;

        using(var f=new FolderBrowserDialog {Description="Choose a folder for red PNG graphics"}) if(f.ShowDialog(this)==DialogResult.OK) {

            string dir=Path.Combine(f.SelectedPath,"BoundHigh-red-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,4));Directory.CreateDirectory(dir);

            Art.Rip(dir);

            status.Text="Red graphics saved · "+dir;

        }

    }

    void SwitchMode(int mode){
        bool pocket=mode==2,score=mode==1;
        if(pocketMode==pocket && scoreMode==score)return;StopStageAudio();pocketMode=pocket;scoreMode=score;adventureMode.Checked=mode==0;scoreMenu.Checked=score;pocketMenu.Checked=pocket;worlds.Visible=mode==0;stageSettings.Visible=!pocket;pocketSettings.Visible=pocket;scoreSettings.Visible=score;foreach(var control in mediaControls)(pocket?pocketSettings:stageSettings).Controls.Add(control);worldMusic.Visible=mode==0;if(!pocket)stageSettings.Controls.SetChildIndex(regular,stageSettings.Controls.Count-1);layer.Visible=!pocket;paletteLayout.RowStyles[2].Height=pocket?0:35;mapLayout.ColumnStyles[1].Width=29*stageSettings.Padding.Left;
        layer.Items.Clear();layer.Items.AddRange(pocket?new object[]{"Terrain","Balls"}:new object[]{"Terrain","Enemies","Powerups","Hidden Powerups"});layer.SelectedIndex=0;
        terrainToggle.Text=pocket?"Terrain":"Terrain/Powerups";enemyToggle.Text=pocket?"Balls":"Enemies";ShowWorld();UpdateTitle();
    }
    void ShowWorld(){stages.Items.Clear();if(Doc==null)return;stages.Items.AddRange(pocketMode?Document.PocketInfo:scoreMode?Document.ScoreInfo:Document.Info.Where(s=>s.World==worlds.SelectedIndex+1).ToArray());stages.SelectedIndex=0;}
    void ChangeStage() {StopStageAudio();worldMusic.Checked=false;if(Stage==null)return;heading.Text=Stage.ToString();RefreshPalette();UpdateRules();canvas.Invalidate();}

    void UpdateRules() {

        if(Stage==null || Doc==null)return;var cells=Doc.Cells(Stage.id);

        changingStage=true;music.SelectedIndex=Doc.Setting(Doc.Data.Music,Stage.id)+1;
        for(int plane=0;plane<2;plane++){int value=Doc.BackgroundChoice(Stage.id,plane);int representative=value<0?value:backgroundAliases[plane][value];backdrops[plane].SelectedIndex=representative==-1?0:representative==-2?1:backdrops[plane].Items.IndexOf(Document.AllStages.Single(t=>t.id==representative));}changingStage=false;
        if(Stage.Pocket){changingStage=true;pocketTime.Value=Doc.PocketRule(Stage.id,0);pocketScore.Value=Doc.PocketRule(Stage.id,1);pocketTimeDefault.Text="Default: "+Doc.PocketDefault(Stage.id,0)+" Seconds";pocketScoreDefault.Text="Default: "+Doc.PocketDefault(Stage.id,1).ToString("N0")+" Points";changingStage=false;electricBrush.Visible=false;rules.Text="Reach the score target before time expires.\nBalls: "+cells.Count(v=>(v>>7)==2)+" / 8.\nLeft drag: paint · Right click: erase layer · Ctrl+Z: undo.\nAdventure and Pocket and Cushion edits are saved together.";return;}
        changingStage=true;if(Stage.ScoreAttack){scoreTarget.Value=Doc.ScoreTarget(Stage.id);scoreDefault.Text="Default: "+Doc.ScoreDefault(Stage.id).ToString("N0")+" Points";}regular.Visible=Stage.mode!=0;regular.Checked=Doc.Regular(Stage.id);
        windStrength.SelectedIndex=Array.IndexOf(Document.Strengths,Doc.Strength(Stage.id)==0?28:Doc.Strength(Stage.id));
        int w=Doc.Setting(Doc.Data.Wind,Stage.id);wind.Checked=w<0?new[]{13,15,24,25,33,37,42,45}.Contains(Stage.id):w==1;
        int g=Doc.Setting(Doc.Data.Gobbler,Stage.id);gobbler.Checked=g<0?Doc.NativeGobbler(Stage):g==1;
        music.SelectedIndex=Doc.Setting(Doc.Data.Music,Stage.id)+1;
        for(int h=0;h<2;h++) {
            bool custom=Doc.Timer(Stage.id,h*2)>=0 || Doc.Timer(Stage.id,h*2+1)>=0;gameTiming[h].Checked=!custom;timingNotes[h].Text=!custom?"":h==0?"Wind starts 2s after the warning.":"Next wait starts after leaving.";

            for(int k=0;k<2;k++){int i=h*2+k,v=Doc.Timer(Stage.id,i);countdown[i].Value=v<0?(h==0?4:k==0?30:24):v/25m;countdown[i].Enabled=custom;}
        }
        changingStage=false;

        int mode=Doc.RenderInfo(Stage).mode;

        string clear=Stage.ScoreAttack?"Reach the score target (enemies may remain)":mode==2?"Plasma stage: pair + / − balls; keep exactly four of each":mode==3?"Post-boss bonus: native bonus-stage rules":"Defeat all enemies; special-stage scripts remain active";

        rules.Text="Clear: "+clear+".\nEnemy slots: "+cells.Count(v=>EnemyPatch.Kind(v>>7)==1)+" / 16 small, "+Document.LargeSlots(cells)+" / 8 large. Large Turtle and Billye use two slots.\nLeft drag: paint · Right click: erase layer · Ctrl+Z: undo · Background is a static preview.";

    }

    void SavePocketRules(){if(changingStage || Stage==null || !Stage.Pocket)return;int points=(int)(Math.Round(pocketScore.Value/100m,MidpointRounding.AwayFromZero)*100);points=Math.Max(100,Math.Min(999900,points));BeginStroke();Doc.SetPocketRules(Stage.id,(int)pocketTime.Value,points);EndStroke();}
    void Audio(bool dumpAll,int choice,IWin32Window owner=null) {
        if(!Ready())return;StopStageAudio();
        using(var form=new AudioDialog(Doc,choice>0?choice:OriginalMusic())) {
            ThemeControl(form,BackColor,palette.BackColor,ForeColor);
            if(dumpAll)form.Shown+=(s,e)=>form.BeginInvoke(new Action(form.DumpAll));
            form.ShowDialog(owner??this);
        }
    }
    int OriginalMusic() {if(Stage.ScoreAttack)return 14;if(Stage.Pocket)return 4;return Doc.RenderInfo(Stage).mode==2 || Doc.RenderInfo(Stage).mode==3?18:Stage.World==1?2:Stage.World==2?10:Stage.World==3?15:12;}
    void AddTimers(FlowLayoutPanel settings,int hazard) {
        var panel=new FlowLayoutPanel {Width=240,AutoSize=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Margin=new Padding(3,0,3,10)};

        var original=new ThemeCheckBox {Text="Use Game Timing",AutoSize=true,Checked=true,AccessibleName=hazard==0?"Wind Game Timing":"Gobbler Game Timing"};gameTiming[hazard]=original;panel.Controls.Add(original);
        audioTips.SetToolTip(original,"Uncheck to choose seconds below. Custom countdowns are the same on every difficulty.");
        for(int k=0;k<2;k++) {
            int index=hazard*2+k;string label=k==0?(hazard==0?"First Warning (s)":"First Appearance (s)"):(hazard==0?"Between Gusts (s)":"After Leaving (s)");
            var row=new FlowLayoutPanel {Width=235,Height=30,WrapContents=false,Margin=new Padding(0,0,0,2)};
            row.Controls.Add(new Label {Text=label,Width=145,Margin=new Padding(0,5,0,0)});
            var number=new NumericUpDown {Minimum=1,Maximum=600,DecimalPlaces=2,Increment=1,Width=82,Value=hazard==0?4:k==0?30:24,AccessibleName=(hazard==0?"Wind ":"Gobbler ")+(k==0?"First Seconds":"Repeat Seconds")};
            countdown[index]=number;row.Controls.Add(number);panel.Controls.Add(row);
            number.ValueChanged+=(s,e)=>SaveTimers(hazard);
        }
        timingNotes[hazard]=new Label {Width=235,Height=20};panel.Controls.Add(timingNotes[hazard]);
        original.CheckedChanged+=(s,e)=>SaveTimers(hazard);settings.Controls.Add(panel);
    }
    void SaveTimers(int hazard) {
        if(changingStage || Stage==null)return;bool original=gameTiming[hazard].Checked;
        BeginStroke();Doc.SetTimers(Stage.id,hazard==0,original?-1:(int)Math.Round(countdown[hazard*2].Value*25,MidpointRounding.AwayFromZero),original?-1:(int)Math.Round(countdown[hazard*2+1].Value*25,MidpointRounding.AwayFromZero));EndStroke();UpdateRules();
    }
    void AddAudioButton(FlowLayoutPanel panel,string icon,string name,Action action) {
        var button=new Button {Text=icon,Width=51,Height=36,AccessibleName=name,Font=new Font("Segoe UI Symbol",12)};
        audioTips.SetToolTip(button,name);button.Click+=(s,e)=>Safe(action);panel.Controls.Add(button);
    }
    void StepTrack(int direction) {
        if(Stage==null)return;int choice=music.SelectedIndex-1;if(choice<1)choice=OriginalMusic();
        music.SelectedIndex=((choice-1+direction+81)%81)+2;
        PlayStageAudio();
    }
    void ApplyMusic() {if(Stage==null)return;BeginStroke();foreach(var stage in Document.AllStages.Where(t=>t.id==Stage.id || (!Stage.Pocket && !Stage.ScoreAttack && !t.Pocket && !t.ScoreAttack && worldMusic.Checked && t.World==Stage.World)))Doc.SetSetting(false,stage.id,music.SelectedIndex-1);EndStroke();}
    void StopStageAudio() {
        if(audioCancel!=null){audioCancel.Cancel();audioCancel=null;}
        status.Text="Audio stopped.";
    }
    async void PlayStageAudio() {
        if(Doc==null || Stage==null)return;StopStageAudio();int choice=music.SelectedIndex-1;
        if(choice==0){status.Text="Silence selected.";return;}if(choice<0)choice=OriginalMusic();
        var operation=new System.Threading.CancellationTokenSource();audioCancel=operation;byte[] rom=Doc.Rom;
        status.Text="Preparing "+new AudioTrack(choice)+"…";
        try {
            var progress=new Progress<string>(value=>{if(!IsDisposed && !operation.IsCancellationRequested && audioCancel==operation)status.Text=value;});
            await System.Threading.Tasks.Task.Run(()=>AudioStream.Play(rom,new AudioTrack(choice),operation.Token,()=>((IProgress<string>)progress).Report("Playing "+new AudioTrack(choice))));
        } catch(OperationCanceledException) {} catch(Exception ex) {if(!IsDisposed && !operation.IsCancellationRequested)MessageBox.Show(this,ex.Message,"Audio",MessageBoxButtons.OK,MessageBoxIcon.Error);}
        finally {if(audioCancel==operation)audioCancel=null;operation.Dispose();}
    }
    void RefreshPalette() {

        electricBrush.Visible=false;
        object selected=palette.SelectedItem;palette.Items.Clear();if(Stage==null)return;

        var choices=Stage.Pocket?new[]{256}.Concat(Document.PocketTerrain.Where(t=>t!=0)):layer.SelectedIndex==1?(Doc.RenderInfo(Stage).mode==0?Enumerable.Range(6,16).Where(c=>c!=8 && c!=10):Enumerable.Empty<int>()).Concat(Doc.RenderInfo(Stage).mode==2?new[]{2,3}:new int[0]):layer.SelectedIndex==3?Enumerable.Range(20,6):layer.SelectedIndex==2?Enumerable.Range(20,6).Concat(new[]{33}):Enumerable.Range(1,43).Where(t=>(t<20 || t>25) && (t<4 || t>6) && t!=15 && t!=18 && t!=19 && t!=34 && t!=32 && t!=33 && t!=38 && t!=39);

        foreach(int t in choices)palette.Items.Add(t);

        if(palette.Items.Count>0)palette.SelectedIndex=selected!=null && palette.Items.Contains(selected)?palette.Items.IndexOf(selected):0;

        canvas.Invalidate();

    }

    static string PocketTileName(int t){switch(t){case 2:return "Floor";case 29:return "Cushion";case 30:return "Pocket (3,000 Points)";case 31:return "Destructible (500 Points)";case 32:return "Bumper (100 Points)";case 34:return "Standalone Cushion";case 36:return "Pocket (10,000 Points)";case 37:return "Pocket (30,000 Points)";default:return "Tile "+t;}}
    static string TileName(int t) {

        switch(t){case 1:return "Breakable";case 2:return "Solid";case 3:case 4:case 5:case 6:return "Pachin Spawner";case 9:return "Mine";case 7:return "Inclined";case 8:return "Electric";case 10:return "Large block · upper left";case 11:return "Large block · upper right";case 12:return "Large block · lower left";case 13:return "Large block · lower right";case 14:return "Black Hole";case 15:return "Special block";case 17:return "Mirror";case 29:return "Large Enemy Hazard";case 30:return "Large Enemy Trap";case 31:return "Enemy Hazard";case 16:return "1/0 Tile";case 20:return "Lucky Star";case 21:return "Powerup";case 22:return "Stop Watch";case 23:return "Super Chalvo";case 24:return "Round Bomb";case 25:return "All Pachins";case 26:return "Horizontal Rope";case 27:return "Vertical Rope";case 28:return "Cross Rope";case 32:return "Unused Platform";case 33:return "Extra Life";
        case 35:return "Trampoline";case 36:return "Capture Arrow (100)";case 37:return "Capture Arrow (300)";case 38:return "Unused Left Arrow";case 39:return "Unused Right Arrow";
        case 40:return "Enemy Route Up";case 41:return "Enemy Route Down";case 42:return "Enemy Route Left";case 43:return "Enemy Route Right";default:return "Tile "+t;}

    }

    internal int BallCount {get{return Doc==null || Stage==null?0:Doc.Cells(Stage.id).Count(v=>(v>>7)==2);}}
    internal int SpawnerCount {get{return Doc==null || Stage==null?0:Doc.Cells(Stage.id).Count(v=>Document.IsSpawner(v));}}

    void DrawPalette(object sender,DrawItemEventArgs e) {

        if(e.Index<0 || Doc==null || Stage==null)return;e.DrawBackground();int t=(int)palette.Items[e.Index];

        e.Graphics.InterpolationMode=InterpolationMode.NearestNeighbor;e.Graphics.PixelOffsetMode=PixelOffsetMode.Half;

        e.Graphics.FillRectangle(Brushes.Black,e.Bounds.X+3,e.Bounds.Y+3,46,46);

        var icon=new Rectangle(e.Bounds.X+3,e.Bounds.Y+3,46,46);

        if(Stage.Pocket && t==256){Art.DrawEnemy(e.Graphics,Stage,2,icon);}else if(!Stage.Pocket && layer.SelectedIndex==1){Art.DrawEnemy(e.Graphics,Stage,t,icon);}else e.Graphics.DrawImage(Art.Tile(t>=40?Document.Info.Single(a=>a.id==7):Stage,t,false),icon);

        string name=Stage.Pocket && t==256?"Ball":!Stage.Pocket && layer.SelectedIndex==1?EnemyPatch.Name(Doc,Stage,t):Stage.Pocket?PocketTileName(t):TileName(t);

        bool ball=Stage.Pocket && t==256,spawner=!Stage.Pocket && layer.SelectedIndex==0 && t==3,full=(spawner && SpawnerCount>=4) || (ball && BallCount>=8);
        if(full)using(var shade=new SolidBrush(Color.FromArgb(160,palette.BackColor)))e.Graphics.FillRectangle(shade,icon);
        Color textColor=full?SystemColors.GrayText:e.ForeColor;
        TextRenderer.DrawText(e.Graphics,name,Font,new Rectangle(e.Bounds.X+56,e.Bounds.Y+3,e.Bounds.Width-56-(spawner || ball?40:0),46),textColor,TextFormatFlags.WordBreak|TextFormatFlags.VerticalCenter);
        if(spawner || ball)TextRenderer.DrawText(e.Graphics,ball?BallCount+"/8":SpawnerCount+"/4",Font,new Rectangle(e.Bounds.Right-40,e.Bounds.Y,36,e.Bounds.Height),textColor,TextFormatFlags.Right|TextFormatFlags.VerticalCenter);
        e.DrawFocusRectangle();

    }

    internal void BeginStroke() {if(Doc!=null)stroke=Doc.Serialize();}

    internal void PaintCell(int i,bool erase) {

        if(Doc==null || Stage==null)return;

        int brush=erase || eraser.Checked?0:(palette.SelectedItem==null?-1:(int)palette.SelectedItem);if(brush<0)return;

        if(Stage.Pocket){
            int value=Doc.Cells(Stage.id)[i];bool ball=brush==256;
            if(ball && (value>>7)==0 && BallCount>=8){status.Text="Maximum eight balls per stage.";return;}
            // In the combined palette, erase the ball first, then the terrain beneath it.
            Doc.Cells(Stage.id)[i]=Document.Paint(value,ball || (brush==0 && (value>>7)!=0),ball?2:brush);
            if(BallCount>=8 && palette.SelectedItem!=null && (int)palette.SelectedItem==256)palette.SelectedIndex=-1;
            palette.Invalidate();canvas.Invalidate();UpdateTitle();UpdateRules();return;
        }
        if(layer.SelectedIndex==1 && brush>=6 && Stage.mode!=0 && !Doc.Regular(Stage.id)){Doc.SetRegular(Stage.id,true);RefreshPalette();}

        var a=Doc.Cells(Stage.id);
        if(layer.SelectedIndex!=1 && brush==3 && !Document.IsSpawner(a[i]) && SpawnerCount>=4){status.Text="Maximum four Pachin Spawners per stage.";return;}
        bool updateSpawners=layer.SelectedIndex!=1 && (brush==3 || Document.IsSpawner(a[i]));

        if(layer.SelectedIndex==1) {

            int size=EnemyPatch.Kind(brush)>=4?2:1,x=i%16,y=i/16;

            if(x+size>16 || y+size>10){status.Text="This enemy needs a 2 × 2 area inside the stage.";return;}

            for(int n=0;n<160;n++) {

                int existing=a[n]>>7,span=EnemyPatch.Kind(existing)>=4?2:1;

                if(existing!=0 && x<n%16+span && x+size>n%16 && y<n/16+span && y+size>n/16){a[n]&=127;Doc.SetElectric(Stage.id,n,-1);}

            }

        }

        if(layer.SelectedIndex==3){
            if(brush!=0 && (Doc.RenderInfo(Stage).mode!=0 || (a[i]&127)!=1)){status.Text="Hidden Powerups require a Breakable tile and regular stage rules.";return;}
            Doc.SetHidden(Stage.id,i,brush==0?-1:brush);canvas.Invalidate();UpdateTitle();return;
        }
        if(layer.SelectedIndex!=1){
            int old=a[i]&127;
            if(brush==1 && old==33){status.Text="Extra Life is not supported as hidden content yet.";return;}
            if(brush==1 && old>=20 && old<=25){if(Doc.RenderInfo(Stage).mode!=0){status.Text="Enable Regular Enemies / Clear Rules before hiding powerups.";return;}Doc.SetHidden(Stage.id,i,old);}
            else if(brush!=1)Doc.SetHidden(Stage.id,i,-1);
        }
        a[i]=Document.Paint(a[i],layer.SelectedIndex==1,brush);
        if(layer.SelectedIndex==1)Doc.SetElectric(Stage.id,i,brush==0 || (brush<6 && Doc.RenderInfo(Stage).mode!=0)?-1:brush==12?1:electricBrush.Checked?1:0);
        if(updateSpawners){if(SpawnerCount<=4)Document.NumberSpawners(a);if(SpawnerCount>=4 && palette.SelectedItem!=null && (int)palette.SelectedItem==3)palette.SelectedIndex=-1;}
        palette.Invalidate();canvas.Invalidate();UpdateTitle();UpdateRules();

    }

    internal void EndStroke() {if(stroke!=null && Doc!=null && stroke!=Doc.Serialize()){undo.Push(stroke);redo.Clear();}stroke=null;UpdateTitle();}

    void History(Stack<string> from,Stack<string> to) {if(Doc==null || from.Count==0)return;StopStageAudio();to.Push(Doc.Serialize());Doc.Load(from.Pop());RefreshPalette();canvas.Invalidate();UpdateTitle();UpdateRules();}

    internal void Hover(int i) {if(Doc!=null && Stage!=null) {int v=Doc.Cells(Stage.id)[i],enemy=v>>7;status.Text=Stage+" · Column "+(i%16+1)+", Row "+(i/16+1)+" · "+TileName(v&127)+" · "+EnemyPatch.Name(Doc,Stage,enemy);}}

    void UpdateTitle() {debugRestoration.Enabled=Doc!=null && Doc.IsProto2;debugRestoration.Checked=Doc!=null && Doc.Data.DebugRestoration;debugRestoration.ToolTipText=Doc!=null && !Doc.IsProto2?"BH.Debug already has debug features; this patch targets Proto 2.":"Restore the game's debug cheats and frame advance.";sram.Enabled=Doc!=null && Doc.IsProto2;sram.Checked=Doc!=null && Doc.Data.Sram;sram.ToolTipText=Doc!=null && !Doc.IsProto2?"This SRAM patch supports Proto 2.":"Save game progress to cartridge SRAM.";stageHud.Enabled=Doc!=null;stageHud.Checked=Doc!=null && Doc.Data.StageHud;bossRush.Enabled=Doc!=null;bossRush.Checked=Doc!=null && Doc.Data.BossRush;Text="BH! Editor v1.0";}

    protected override void Dispose(bool disposing) {if(disposing){StopStageAudio();audioTips.Dispose();eraser.Dispose();if(Art!=null)Art.Dispose();}base.Dispose(disposing);}

}



// Keep native checkbox keyboard/accessibility behavior, with a larger painted glyph.
class ThemeCheckBox : CheckBox {
    internal ThemeCheckBox(){AutoSize=true;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
    protected override void OnCheckStateChanged(EventArgs e){base.OnCheckStateChanged(e);Invalidate();}
    protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
    public override Size GetPreferredSize(Size proposedSize){var text=TextRenderer.MeasureText(Text,Font);return new Size(text.Width+29,Math.Max(28,text.Height+6));}
    protected override void OnPaint(PaintEventArgs e){
        e.Graphics.Clear(BackColor);int size=Math.Max(18,(int)(18*e.Graphics.DpiX/96)),y=(Height-size)/2;
        Color color=Enabled?ForeColor:SystemColors.GrayText;
        using(var pen=new Pen(color)){e.Graphics.DrawRectangle(pen,1,y,size,size);}
        if(Checked)using(var pen=new Pen(color,2))e.Graphics.DrawLines(pen,new[]{new Point(4,y+size/2),new Point(1+size/2,y+size-4),new Point(size-2,y+4)});
        TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(size+8,0,Width-size-8,Height),color,TextFormatFlags.Left|TextFormatFlags.VerticalCenter);
        if(Focused && ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,ClientRectangle,color,BackColor);
    }
}

sealed class HazardCheckBox : ThemeCheckBox {
    internal HazardCheckBox(){Font=new Font("Segoe UI",11);}
}

sealed class MenuRenderer : ToolStripProfessionalRenderer {
    readonly bool dark;
    internal MenuRenderer(bool value):base(new ThemeColors(value)){dark=value;RoundedEdges=false;}
    static bool RightCheck(ToolStripItem item){return (string)item.Tag=="RightCheck";}
    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e) {
        if(!dark){base.OnRenderMenuItemBackground(e);return;}
        using(var brush=new SolidBrush(e.Item.Selected || e.Item.Pressed?Color.FromArgb(55,55,55):e.Item.BackColor))
            e.Graphics.FillRectangle(brush,new Rectangle(Point.Empty,e.Item.Size));
    }
    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e) {if(!RightCheck(e.Item))base.OnRenderItemCheck(e);}
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) {
        base.OnRenderItemText(e);
        if(!RightCheck(e.Item))return;
        int size=Math.Max(12,(int)(13*e.Graphics.DpiX/96)),x=e.Item.Width-size-8,y=(e.Item.Height-size)/2;
        using(var pen=new Pen(e.Item.ForeColor))e.Graphics.DrawRectangle(pen,x,y,size,size);
        if(((ToolStripMenuItem)e.Item).Checked)using(var pen=new Pen(e.Item.ForeColor,2))
            e.Graphics.DrawLines(pen,new[]{new Point(x+3,y+size/2),new Point(x+size/2,y+size-3),new Point(x+size-2,y+3)});
    }
}

sealed class ThemeColors : ProfessionalColorTable {

    readonly bool dark;

    internal ThemeColors(bool value){dark=value;UseSystemColors=!value;}

    Color Back {get{return Color.FromArgb(29,31,35);}}
    public override Color ToolStripDropDownBackground {get{return dark?Back:base.ToolStripDropDownBackground;}}
    public override Color ImageMarginGradientBegin {get{return dark?Back:base.ImageMarginGradientBegin;}}
    public override Color ImageMarginGradientMiddle {get{return dark?Back:base.ImageMarginGradientMiddle;}}
    public override Color ImageMarginGradientEnd {get{return dark?Back:base.ImageMarginGradientEnd;}}
    public override Color MenuBorder {get{return dark?Color.FromArgb(75,75,75):base.MenuBorder;}}

    public override Color ToolStripGradientBegin {get{return dark?Back:base.ToolStripGradientBegin;}}

    public override Color ToolStripGradientMiddle {get{return dark?Back:base.ToolStripGradientMiddle;}}

    public override Color ToolStripGradientEnd {get{return dark?Back:base.ToolStripGradientEnd;}}

    public override Color StatusStripGradientBegin {get{return dark?Back:base.StatusStripGradientBegin;}}

    public override Color StatusStripGradientEnd {get{return dark?Back:base.StatusStripGradientEnd;}}

    public override Color ButtonSelectedGradientBegin {get{return dark?Color.FromArgb(65,69,77):base.ButtonSelectedGradientBegin;}}

    public override Color ButtonSelectedGradientMiddle {get{return dark?Color.FromArgb(65,69,77):base.ButtonSelectedGradientMiddle;}}

    public override Color ButtonSelectedGradientEnd {get{return dark?Color.FromArgb(65,69,77):base.ButtonSelectedGradientEnd;}}

}



static class Program {

    [STAThread] static int Main(string[] args) {

        if(args.Length>0 && args[0]=="--self-test")Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);

        try {

            if(args.Length>0 && args[0]=="--cross-test") {SelfTest.Cross(args[1],args[2]);return 0;}

            if(args.Length>0 && args[0]=="--self-test") {SelfTest.Run(args[1],args[2]);return 0;}

            if(args.Length>0 && args[0]=="--rip") {using(var a=new Artwork(new Document(args[1])))a.Rip(args[2]);return 0;}
            if(args.Length>0 && args[0]=="--dump-audio") {AudioRenderer.Dump(new Document(args[1]).Rom,args[2],System.Threading.CancellationToken.None,null);return 0;}
            if(args.Length>0 && args[0]=="--audio-test") {SelfTest.Audio(args[1],args[2]);return 0;}

            string rom=args.Length>0?args[0]:new[]{
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Bound High (World) (Proto 2).vb"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","Bound High (World) (Proto 2).vb"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"BH.Debug.vb"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","BH.Debug.vb")
            }.FirstOrDefault(File.Exists);

            Application.Run(new Editor(File.Exists(rom)?rom:null));return 0;

        } catch(Exception ex) {

            if(args.Length>0 && (args[0]=="--self-test" || args[0]=="--cross-test" || args[0]=="--dump-audio" || args[0]=="--audio-test")) {Directory.CreateDirectory(args[2]);File.WriteAllText(Path.Combine(args[2],"test-failure.txt"),ex.ToString());return 1;}

            MessageBox.Show(ex.Message,"Bound High Editor",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;

        }

    }

}
