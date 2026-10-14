using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

static class SelfTest {
    internal static void Audio(string rom,string output) {
        Directory.CreateDirectory(output);var d=new Document(rom);

        long first=-1;var clock=System.Diagnostics.Stopwatch.StartNew();
        using(var cancellation=new System.Threading.CancellationTokenSource()) {
            try{AudioStream.Play(d.Rom,new AudioTrack(2),cancellation.Token,()=>{first=clock.ElapsedMilliseconds;cancellation.CancelAfter(250);});}catch(OperationCanceledException){}
            Check(first>=0 && clock.ElapsedMilliseconds-first<1500,"Streaming start/cancellation failed.");
        }
        File.WriteAllText(Path.Combine(output,"stream-latency.txt"),"First playback buffer: "+first+" ms; cancelled and device released at "+clock.ElapsedMilliseconds+" ms.\r\n");
        Reject(()=>new AudioRenderer(new byte[100]),"Wrong ROM accepted for audio.");
        Reject(()=>new AudioTrack(0),"Silence treated as stored audio.");
        using(var renderer=new AudioRenderer(d.Rom)) {
            var effect=renderer.Render(new AudioTrack(49),System.Threading.CancellationToken.None);
            Check(System.Text.Encoding.ASCII.GetString(effect.Wave,0,4)=="RIFF" && BitConverter.ToInt32(effect.Wave,24)==41700,"Invalid WAV header.");
            Check(BitConverter.ToInt32(effect.Wave,40)==effect.Wave.Length-44 && effect.Wave.Length%4==0,"Invalid WAV size/alignment.");
            Check(effect.Wave.Skip(44).Any(b=>b!=0) && effect.Ending=="Natural End","Effect rendering failed.");
            d.Write(Path.Combine(output,"effect.wav"),effect.Wave,".wav");
            Reject(()=>d.Write(rom,effect.Wave,".wav"),"Audio can overwrite source ROM.");
            Reject(()=>d.Write(Path.Combine(output,"bad.vb"),effect.Wave,".wav"),"Audio export accepted ROM extension.");
            using(var cancel=new System.Threading.CancellationTokenSource()) {
                cancel.CancelAfter(10);bool cancelled=false;
                try{renderer.Render(new AudioTrack(10),cancel.Token);}catch(OperationCanceledException){cancelled=true;}
                Check(cancelled,"Rendering ignores cancellation.");
            }
            var again=renderer.Render(new AudioTrack(49),System.Threading.CancellationToken.None);
            Check(effect.Wave.SequenceEqual(again.Wave),"Cancellation or previous tracks contaminate the next sound.");
            var silent=renderer.Render(new AudioTrack(42),System.Threading.CancellationToken.None);
            Check(silent.Wave.Skip(44).All(b=>b==0),"Boot sound leaks into isolated effects.");
            var music=renderer.Render(new AudioTrack(6),System.Threading.CancellationToken.None);
            Check(music.Ending=="Two Loops + Fade" && music.Seconds>10 && music.Wave.Skip(music.Wave.Length-4).All(b=>b==0),"Music loop/fade failed.");
            d.Write(Path.Combine(output,"music.wav"),music.Wave,".wav");
            using(var player=new System.Media.SoundPlayer(new MemoryStream(music.Wave)))player.Load();
        }
        using(var editor=new Editor(rom))using(var dialog=new AudioDialog(d,42)) {
            editor.ThemeControl(dialog,editor.BackColor,Color.FromArgb(39,42,47),editor.ForeColor);
            dialog.Show();Application.DoEvents();
            var list=Controls(dialog).OfType<ListBox>().Single();Check(list.Items.Count==81,"Audio list incomplete.");
            var play=Controls(dialog).OfType<Button>().Single(b=>b.Text=="Play");var stop=Controls(dialog).OfType<Button>().Single(b=>b.Text=="Stop");
            dialog.BeginInvoke(new Action(()=>play.PerformClick()));Application.DoEvents();var timer=System.Diagnostics.Stopwatch.StartNew();
            while(!play.Enabled && timer.ElapsedMilliseconds<15000){Application.DoEvents();System.Threading.Thread.Sleep(10);}
            Check(play.Enabled,"Audio preview did not finish preparing.");
            Check(Controls(dialog).OfType<Label>().Any(l=>l.Text.StartsWith("Playing ")),"Audio preview failed.");stop.PerformClick();
            Check(dialog.BackColor.GetBrightness()<0.2f,"Audio dialog must inherit the dark theme.");
            using(var image=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(image,new Rectangle(0,0,dialog.Width,dialog.Height));image.Save(Path.Combine(output,"audio-dialog.png"),ImageFormat.Png);}
            list.SelectedIndex=9;dialog.BeginInvoke(new Action(()=>{play.PerformClick();dialog.Close();}));Application.DoEvents();timer.Restart();
            while(!dialog.IsDisposed && timer.ElapsedMilliseconds<15000){Application.DoEvents();System.Threading.Thread.Sleep(10);}
            Check(dialog.IsDisposed,"Closing during audio rendering did not cancel safely.");
        }
        using(var editor=new Editor(rom)) {
            editor.Show();Application.DoEvents();
            var bgm=Controls(editor).OfType<ComboBox>().Single(c=>c.AccessibleName=="Stage BGM");
            var buttons=Controls(editor).OfType<Button>().Where(b=>new[]{"Previous Track","Play","Stop","Next Track"}.Contains(b.AccessibleName)).ToArray();
            Check(buttons.Length==4,"Inline playback controls missing.");
            buttons.Single(b=>b.AccessibleName=="Next Track").PerformClick();Check(bgm.SelectedIndex==4,"Next track did not advance from native World 1 music.");
            bgm.SelectedIndex=2;buttons.Single(b=>b.AccessibleName=="Previous Track").PerformClick();Check(bgm.SelectedIndex==82,"Previous track wrap failed.");
            buttons.Single(b=>b.AccessibleName=="Next Track").PerformClick();Check(bgm.SelectedIndex==2,"Next track wrap failed.");
            bgm.SelectedIndex=42;
            editor.BeginInvoke(new Action(()=>buttons.Single(b=>b.AccessibleName=="Next Track").PerformClick()));Application.DoEvents();
            var autoplayStatus=Controls(editor).OfType<StatusStrip>().Single().Items[0];var autoTimer=System.Diagnostics.Stopwatch.StartNew();
            while(autoplayStatus.Text.StartsWith("Preparing") && autoTimer.ElapsedMilliseconds<15000){Application.DoEvents();System.Threading.Thread.Sleep(10);}
            Check(bgm.SelectedIndex==43 && autoplayStatus.Text.StartsWith("Playing"),"Next Track did not autoplay.");
            bgm.SelectedIndex=44;
            editor.BeginInvoke(new Action(()=>buttons.Single(b=>b.AccessibleName=="Previous Track").PerformClick()));Application.DoEvents();autoTimer.Restart();
            while(autoplayStatus.Text.StartsWith("Preparing") && autoTimer.ElapsedMilliseconds<15000){Application.DoEvents();System.Threading.Thread.Sleep(10);}
            Check(bgm.SelectedIndex==43 && autoplayStatus.Text.StartsWith("Playing"),"Previous Track did not autoplay.");
            bgm.SelectedIndex=43; // silent effect 17, fast deterministic preview
            editor.BeginInvoke(new Action(()=>buttons.Single(b=>b.AccessibleName=="Play").PerformClick()));Application.DoEvents();
            var status=Controls(editor).OfType<StatusStrip>().Single().Items[0];var timer=System.Diagnostics.Stopwatch.StartNew();
            while(status.Text.StartsWith("Preparing") && timer.ElapsedMilliseconds<15000){Application.DoEvents();System.Threading.Thread.Sleep(10);}
            Check(status.Text.StartsWith("Playing"),"Inline ROM audio preview failed.");
            buttons.Single(b=>b.AccessibleName=="Stop").PerformClick();Check(status.Text=="Audio stopped.","Inline Stop failed.");
            bgm.SelectedIndex=11;editor.BeginInvoke(new Action(()=>{buttons.Single(b=>b.AccessibleName=="Play").PerformClick();buttons.Single(b=>b.AccessibleName=="Stop").PerformClick();}));Application.DoEvents();
            timer.Restart();while(timer.ElapsedMilliseconds<300){Application.DoEvents();System.Threading.Thread.Sleep(10);}
            Check(status.Text=="Audio stopped.","Cancelled render resumed playback.");
        }
        Check(Document.Digest(File.ReadAllBytes(rom))==d.SourceHash,"Audio changed source ROM.");
        File.WriteAllText(Path.Combine(output,"audio-test-results.txt"),"PASS: fresh-ROM audio; valid WAV headers; audible effect; silent control without boot leakage; deterministic restart; cancellation and dialog close; 81-entry preview UI; Play/Stop; two-loop fade; WAV loading; original-ROM/extension protection.\r\n");
    }
    internal static void Cross(string rom,string output) {
        Directory.CreateDirectory(output);
        for(int code=6;code<22;code++) {
            var d=new Document(rom);
            foreach(var s in Document.Info) {
                if(s.mode!=0)d.SetRegular(s.id,true);
                for(int i=0;i<160;i++)d.Cells(s.id)[i]=2;
                d.Cells(s.id)[17]=2|(code<<7);
            }
            File.WriteAllBytes(Path.Combine(output,"enemy-"+(code-5)+".vb"),d.Export());
        }
        int[][] mixes={new[]{7,9,11,12,14,15,18,19},new[]{20,21,7,12,15,18}};
        for(int n=0;n<mixes.Length;n++) {
            var d=new Document(rom);
            foreach(var s in Document.Info) {
                if(s.mode!=0)d.SetRegular(s.id,true);
                for(int i=0;i<160;i++)d.Cells(s.id)[i]=2;
                for(int i=0;i<mixes[n].Length;i++)d.Cells(s.id)[17+i]=2|(mixes[n][i]<<7);
                for(int i=80;i<96;i++)d.Cells(s.id)[i]=2|(6<<7);
            }
            File.WriteAllBytes(Path.Combine(output,"mixed-"+n+".vb"),d.Export());
        }
    }
    static System.Collections.Generic.IEnumerable<Control> Controls(Control c) {foreach(Control child in c.Controls){yield return child;foreach(var nested in Controls(child))yield return nested;}}
    static void Check(bool ok,string msg) {if(!ok)throw new Exception(msg);}
    static void Reject(Action a,string message) {bool rejected=false;try{a();}catch{rejected=true;}Check(rejected,message);}
    internal static void Run(string rom,string output) {
        Directory.CreateDirectory(output);
        if(File.Exists(Path.Combine(output,"test-failure.txt")))File.Delete(Path.Combine(output,"test-failure.txt"));
        var d=new Document(rom);string clean=d.Serialize();
        Check(Document.Info.Length==45 && Document.Info.GroupBy(s=>s.World).Select(g=>g.Count()).SequenceEqual(new[]{11,11,11,12}),"Four-world stage coverage.");
        var preDebug=Document.Json.Deserialize<Project>(clean);preDebug.Version=16;
        d.Load(Document.Json.Serialize(preDebug));Check(!d.Data.DebugRestoration && d.Export().SequenceEqual(d.Rom),"v16 migration enables Debug Restoration.");
        preDebug.DebugRestoration=true;Reject(()=>d.Validate(preDebug),"Old project accepted debug flag.");d.Load(clean);
        if(!d.IsProto2){d.Data.DebugRestoration=true;Reject(()=>d.Export(),"Wrong ROM accepted debug patch.");d.Load(clean);}
        var preSram=Document.Json.Deserialize<Project>(clean);preSram.Version=15;
        d.Load(Document.Json.Serialize(preSram));Check(!d.Data.Sram && d.Export().SequenceEqual(d.Rom),"v15 migration enables SRAM.");
        preSram.Sram=true;Reject(()=>d.Validate(preSram),"Old project accepted SRAM flag.");d.Load(clean);
        if(!d.IsProto2){d.Data.Sram=true;Reject(()=>d.Export(),"Wrong ROM accepted SRAM patch.");d.Load(clean);}
        var previous=Document.Json.Deserialize<Project>(clean);previous.Version=13;
        d.Load(Document.Json.Serialize(previous));Check(!d.Data.BossRush && d.Export().SequenceEqual(d.Rom),"v13 migration enables Boss Rush.");
        previous.BossRush=true;Reject(()=>d.Validate(previous),"Old project accepted enhancement flag.");d.Load(clean);
        var legacy=Document.Json.Deserialize<Project>(clean);legacy.Version=1;legacy.ScoreStages=null;
        foreach(var key in legacy.Stages.Keys.Where(k=>int.Parse(k)>=12).ToArray())legacy.Stages.Remove(key);
        legacy.Stages["0"][1]=0;d.Cells(12)[0]=0;
        d.Load(Document.Json.Serialize(legacy));Check(d.Cells(0)[1]==0 && d.Data.Version==18,"World 1 project migration lost edits.");
        Check(d.Cells(12).SequenceEqual(new Document(rom).Cells(12)),"Migration must initialize other worlds from original ROM.");d.Load(clean);
        foreach(int sid in new[]{12,24,36,46,48}) {
            int cell=Array.FindIndex(d.Cells(sid),v=>(v&127)!=0);Check(cell>=0,"New stage must have terrain.");
            d.Cells(sid)[cell]&=~127;var bytes=d.Export();int at=d.Offset(sid)+cell*2;
            Check(Enumerable.Range(0,bytes.Length).Where(i=>bytes[i]!=d.Rom[i]).SequenceEqual(new[]{at}),"New-world edit changed other ROM data.");
            string project=d.Serialize();d.Load(clean);d.Load(project);Check((d.Cells(sid)[cell]&127)==0,"New-world project roundtrip failed.");
            d.Write(Path.Combine(output,"stage-id-"+sid+".vb"),bytes,false);d.Load(clean);
        }
        Check(d.Rom.SequenceEqual(d.Export()),"Untouched export must be byte-identical.");
        var wrongRevision=Document.Json.Deserialize<Project>(clean);wrongRevision.BaseSha1=d.IsProto2?Document.Hash:Proto2.Hash;Reject(()=>d.Validate(wrongRevision),"Project accepted a different ROM engine.");
        foreach(var stage in Document.Info){d.SetBackground(stage.id,0,12);d.SetBackground(stage.id,1,24);}
        File.WriteAllBytes(Path.Combine(output,"backgrounds.vb"),d.Export());d.Load(clean);
        foreach(var stage in Document.Info){d.SetBackground(stage.id,0,-2);d.SetBackground(stage.id,1,-2);}
        File.WriteAllBytes(Path.Combine(output,"backgrounds-none.vb"),d.Export());d.Load(clean);
        foreach(var stage in Document.Info){if(stage.mode!=0)d.SetRegular(stage.id,true);for(int i=0;i<160;i++)d.Cells(stage.id)[i]=2;d.Cells(stage.id)[17]=2|(6<<7);d.Cells(stage.id)[21]=2|(7<<7);d.Cells(stage.id)[25]=2|(20<<7);d.Cells(stage.id)[65]=2|(21<<7);foreach(int cell in new[]{17,21,25,65})d.SetElectric(stage.id,cell,1);}
        File.WriteAllBytes(Path.Combine(output,"electric-on.vb"),d.Export());
        foreach(var stage in Document.Info)foreach(int cell in new[]{17,21,25,65})d.SetElectric(stage.id,cell,0);
        File.WriteAllBytes(Path.Combine(output,"electric-off.vb"),d.Export());d.Load(clean);

        for(int i=0;i<5;i++)d.Cells(0)[i]=3;
        Reject(()=>d.Export(),"More than four spawners accepted.");d.Load(clean);
        d.Cells(0)[0]=3;d.Cells(0)[1]=3;
        Reject(()=>d.Export(),"Duplicate spawner IDs accepted.");d.Load(clean);

        var version4=Document.Json.Deserialize<Project>(clean);version4.Version=4;version4.ScoreStages=null;d.Load(Document.Json.Serialize(version4));
        Check(d.Data.Version==18 && d.Data.Gobbler==null && d.Rom.SequenceEqual(d.Export()),"Version-4 migration changed ROM behavior.");d.Load(clean);
        var version5=Document.Json.Deserialize<Project>(clean);version5.Version=5;version5.ScoreStages=null;d.Load(Document.Json.Serialize(version5));Check(d.Data.Version==18 && d.Rom.SequenceEqual(d.Export()),"Version-5 timer migration changed the ROM.");d.Load(clean);
        foreach(var stage in Document.Info){d.SetSetting(true,stage.id,1);d.SetGobbler(stage.id,1);d.SetTimers(stage.id,true,50,75);d.SetTimers(stage.id,false,100,125);}
        File.WriteAllBytes(Path.Combine(output,"timers-all.vb"),d.Export());string timerProject=d.Serialize();d.Load(clean);d.Load(timerProject);
        Check(Document.Info.All(t=>d.Timer(t.id,0)==50 && d.Timer(t.id,1)==75 && d.Timer(t.id,2)==100 && d.Timer(t.id,3)==125),"Timer project roundtrip failed.");d.Load(clean);
        var badTimer=Document.Json.Deserialize<Project>(timerProject);badTimer.Timers["0"][0]=15001;Reject(()=>d.Load(Document.Json.Serialize(badTimer)),"Out-of-range timer accepted.");
        badTimer.Timers["0"]=new[]{25};Reject(()=>d.Load(Document.Json.Serialize(badTimer)),"Malformed timer record accepted.");
        d.SetTimers(0,true,25,15000);d.SetTimers(0,false,15000,25);File.WriteAllBytes(Path.Combine(output,"timer-limits.vb"),d.Export());
        d.SetTimers(0,true,-1,-1);d.SetTimers(0,false,-1,-1);Check(d.Rom.SequenceEqual(d.Export()),"Restoring game timing did not restore original export.");d.Load(clean);
        foreach(var stage in Document.Info)d.SetGobbler(stage.id,1);
        File.WriteAllBytes(Path.Combine(output,"gobbler-on.vb"),d.Export());
        string hazards=d.Serialize();d.Load(clean);d.Load(hazards);
        Check(Document.Info.All(t=>d.Setting(d.Data.Gobbler,t.id)==1),"Gobbler project roundtrip failed.");
        foreach(var stage in Document.Info)d.SetGobbler(stage.id,0);
        File.WriteAllBytes(Path.Combine(output,"gobbler-off.vb"),d.Export());d.Load(clean);
        var invalidHazard=Document.Json.Deserialize<Project>(clean);invalidHazard.Gobbler=new System.Collections.Generic.Dictionary<string,int>{{"0",2}};
        Reject(()=>d.Load(Document.Json.Serialize(invalidHazard)),"Invalid Gobbler value accepted.");
        invalidHazard.Gobbler=new System.Collections.Generic.Dictionary<string,int>{{"10",1}};
        Reject(()=>d.Load(Document.Json.Serialize(invalidHazard)),"Boss Gobbler override accepted.");

        foreach(var stage in Document.Info) {
            d.SetSetting(true,stage.id,1);d.SetSetting(false,stage.id,10);
        }
        File.WriteAllBytes(Path.Combine(output,"settings-all.vb"),d.Export());
        string settings=d.Serialize();d.Load(clean);d.Load(settings);
        Check(d.Setting(d.Data.Wind,0)==1 && d.Setting(d.Data.Music,48)==10,"Stage settings roundtrip failed.");d.Load(clean);
        foreach(var stage in Document.Info) {
            if(stage.mode!=0)d.SetRegular(stage.id,true);
            for(int i=0;i<160;i++)d.Cells(stage.id)[i]=i<44?i:2;
            d.Cells(stage.id)[100]|=6<<7;
        }
        File.WriteAllBytes(Path.Combine(output,"terrain-all.vb"),d.Export());d.Load(clean);
        Check(Document.Paint(130,false,0)==128,"Terrain erase must preserve the enemy.");
        Check(Document.Paint(130,true,0)==2,"Enemy erase must preserve terrain.");
        Check(Document.Paint(2,true,1)==130,"Enemy painting encoding.");
        int index=Array.FindIndex(d.Cells(0),v=>v==2);Check(index>=0,"Editable original cell.");
        d.Cells(0)[index]=0;byte[] patched=d.Export();
        int offset=d.Offset(0)+index*2;
        Check(Enumerable.Range(0,patched.Length).Where(i=>patched[i]!=d.Rom[i]).SequenceEqual(new[]{offset}),"Export modified bytes outside the selected cell.");
        string modified=d.Serialize();d.Load(clean);d.Load(modified);Check(d.Cells(0)[index]==0,"Project roundtrip.");
        d.Write(Path.Combine(output,"smoke.bhproj"),System.Text.Encoding.UTF8.GetBytes(modified),true);
        d.Load(File.ReadAllText(Path.Combine(output,"smoke.bhproj")));Check(d.Serialize()==modified,"Saved project mismatch.");
        d.Write(Path.Combine(output,"smoke.vb"),d.Export(),false);
        Reject(()=>d.Write(rom,patched,false),"Original ROM path not protected.");
        Reject(()=>d.Load("{}"),"Malformed project accepted.");
        var bad=Document.Json.Deserialize<Project>(clean);bad.Stages.Remove("0");bad.Stages["10"]=new int[160];
        Reject(()=>d.Validate(bad),"Boss stage accepted.");
        bad=Document.Json.Deserialize<Project>(clean);bad.Stages["0"][0]=65535;
        Reject(()=>d.Validate(bad),"Unknown tile accepted.");
        d.Load(clean);for(int i=0;i<17;i++)d.Cells(0)[i]=130;
        Reject(()=>d.Export(),"Enemy capacity not checked.");d.Load(clean);
        for(int i=0;i<5;i++)d.Cells(0)[i]=(4<<7)|2;
        Reject(()=>d.Export(),"Two-slot enemy capacity not checked.");d.Load(clean);
        d.Cells(0)[index]=20;Check(d.Export()[offset]==20,"Powerup outside original palette failed.");d.Load(clean);
        d.Cells(0)[index]=(3<<7)|7;Check(d.Export()[offset]==135,"Native enemy/terrain outside original palette failed.");d.Load(clean);
        int ball=Array.FindIndex(d.Cells(5),v=>(v>>7)==2);d.Cells(5)[ball]&=127;
        Reject(()=>d.Export(),"Unsafe Plasma ball count accepted.");d.Load(clean);
        d.Cells(7)[0]=2|(4<<7);Reject(()=>d.Export(),"Enemy with incompatible stage graphics accepted.");d.Load(clean);
        d.Cells(7)[0]=2|(20<<7);Check(d.Export().Length==2097152,"Imported large enemy must work in special-bank stages.");
        d.Load(clean);d.SetRegular(5,true);d.Cells(5)[ball]=2|(18<<7);
        string converted=d.Serialize();d.Load(clean);d.Load(converted);Check(d.Regular(5) && d.Cells(5)[ball]>>7==18,"Converted bonus project lost its enemy or rules.");
        using(var art=new Artwork(d)) {
            var bonus=Document.Info.Single(s=>s.id==5);var regularArt=art.Background(bonus);
            d.SetRegular(5,false);Check(!object.ReferenceEquals(regularArt,art.Background(bonus)),"Bonus mode change reused stale graphics.");
        }
        Reject(()=>d.Export(),"Imported enemy accepted with native Plasma initialization.");d.Load(clean);
        bad=Document.Json.Deserialize<Project>(clean);bad.RegularStages=new[]{0};Reject(()=>d.Validate(bad),"Invalid bonus conversion accepted.");
        bad=Document.Json.Deserialize<Project>(clean);bad.Stages["0"][0]=2|(22<<7);Reject(()=>d.Validate(bad),"Unknown global enemy accepted.");
        bad=Document.Json.Deserialize<Project>(clean);bad.Version=2;bad.ScoreStages=null;d.Load(Document.Json.Serialize(bad));Check(d.Data.Version==18,"Version-2 project migration failed.");d.Load(clean);
        for(int i=0;i<17;i++)d.Cells(0)[i]=2|(6<<7);Reject(()=>d.Export(),"Imported small-enemy capacity not checked.");d.Load(clean);
        for(int i=0;i<5;i++)d.Cells(0)[i]=2|(20<<7);Reject(()=>d.Export(),"Imported two-slot capacity not checked.");d.Load(clean);
        using(var art=new Artwork(d))foreach(var stage in Document.Info)foreach(var entry in stage.enemyPoses) {
            int type=int.Parse(entry.Key);var sprite=art.Tile(stage,type,true);
            Check(sprite.Width>8 && sprite.Height>8,"Fragmentary enemy preview: "+stage.id+":"+type);
            if(type>=4)Check(sprite.Width>32 && sprite.Height>32,"Large enemy was cropped to a single tile.");
            if(type==1)Check(entry.Value.parts.Any(p=>p[3]==0) && entry.Value.parts.Any(p=>p[3]==1),"Small actor must combine body and eyes.");
            for(int y=0;y<sprite.Height;y++)for(int x=0;x<sprite.Width;x++){var pixel=sprite.GetPixel(x,y);Check(pixel.A==0 || Artwork.Red.Any(c=>c.ToArgb()==pixel.ToArgb()),"Enemy rip contains non-palette colors.");}
        }
        for(int i=0;i<160;i++)d.Cells(0)[i]=i<44?i:2;
        for(int t=1;t<=5;t++)d.Cells(0)[112+t*2]=2|(t<<7);
        d.Write(Path.Combine(output,"native-palette.vb"),d.Export(),false);d.Load(clean);
        using(var a=new Artwork(d))foreach(var s in Document.Info.Where(s=>s.scriptedTiles!=null)) {
            Check(EnemyPatch.TerrainDonor(d,s,35).id==0,"Slot-stage trampoline uses the machine's graphics bank.");
            int originalCell=d.Cells(s.id)[100];d.Cells(s.id)[100]=35;
            d.Write(Path.Combine(output,"slot-trampoline-"+s.id+".vb"),d.Export(),false);
            d.Cells(s.id)[100]=originalCell;
            Check(s.scriptedTiles.Count==11,"Incomplete slot-machine/hole overlay.");
            using(var overlay=new Bitmap(512,320))using(var g=Graphics.FromImage(overlay)) {
                foreach(var entry in s.scriptedTiles){
                    int cell=int.Parse(entry.Key),original=d.Cells(s.id)[cell];var tile=a.TerrainAt(s,cell);
                    g.DrawImageUnscaled(tile,cell%16*32,cell/16*32);
                    d.Cells(s.id)[cell]=0;
                    Check(object.ReferenceEquals(tile,a.TerrainAt(s,cell)),"Stored terrain incorrectly removes a scripted overlay.");
                    d.Cells(s.id)[cell]=original;
                }
                overlay.Save(Path.Combine(output,"slot-overlay-"+s.id+".png"),ImageFormat.Png);
            }
        }
        using(var a=new Artwork(d))foreach(var s in Document.Info)using(var b=a.Map(s)) {
            for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++)Check(Artwork.Red.Any(c=>c.ToArgb()==b.GetPixel(x,y).ToArgb()),"Non-hardware-palette map color.");
            b.Save(Path.Combine(output,"stage-"+(s.id+1)+".png"),ImageFormat.Png);
        }
        using(var f=new Editor(rom)) {
            f.Show();Application.DoEvents();
            Check(!f.bossRush.Checked,"Boss Rush must start disabled.");
            f.bossRush.PerformClick();Check(f.Doc.Data.BossRush && f.bossRush.Checked,"Boss Rush toggle failed.");
            string rushProject=f.Doc.Serialize();f.Doc.Load(rushProject);Check(f.Doc.Data.BossRush,"Boss Rush persistence failed.");
            var enhancementTools=(ToolStripMenuItem)f.MainMenuStrip.Items[2];
            enhancementTools.DropDownItems[0].PerformClick();Check(!f.Doc.Data.BossRush && !f.bossRush.Checked,"Boss Rush undo failed.");
            enhancementTools.DropDownItems[1].PerformClick();Check(f.Doc.Data.BossRush && f.bossRush.Checked,"Boss Rush redo failed.");
            byte[] rush=f.Doc.Export();Check(rush.Length==2097152 && rush.Skip(EnemyPatch.PatchEnd).SequenceEqual(f.Doc.Rom.Skip(EnemyPatch.PatchEnd)),"Boss Rush changes cartridge size/header.");
            File.WriteAllBytes(Path.Combine(output,"boss-rush.vb"),rush);
            f.Doc.SetSetting(false,0,10);File.WriteAllBytes(Path.Combine(output,"boss-rush-mixed.vb"),f.Doc.Export());f.Doc.Load(rushProject);
            f.bossRush.PerformClick();Check(f.Doc.Export().SequenceEqual(f.Doc.Rom),"Disabling Boss Rush leaves patches behind.");
            Check(!f.stageHud.Checked,"Stage HUD must start disabled.");
            f.stageHud.PerformClick();Check(f.Doc.Data.StageHud && f.stageHud.Checked,"Stage HUD toggle failed.");
            string hudProject=f.Doc.Serialize();f.Doc.Load(hudProject);Check(f.Doc.Data.StageHud,"Stage HUD persistence failed.");
            enhancementTools.DropDownItems[0].PerformClick();Check(!f.Doc.Data.StageHud && !f.stageHud.Checked,"Stage HUD undo failed.");
            enhancementTools.DropDownItems[1].PerformClick();Check(f.Doc.Data.StageHud && f.stageHud.Checked,"Stage HUD redo failed.");
            byte[] hud=f.Doc.Export();Check(hud.Length==2097152 && hud.Skip(EnemyPatch.PatchEnd).SequenceEqual(f.Doc.Rom.Skip(EnemyPatch.PatchEnd)),"Stage HUD changes cartridge size/header.");
            File.WriteAllBytes(Path.Combine(output,"stage-hud.vb"),hud);
            f.bossRush.PerformClick();f.Doc.SetSetting(false,0,10);
            File.WriteAllBytes(Path.Combine(output,"stage-hud-boss-mixed.vb"),f.Doc.Export());
            f.Doc.Load(hudProject);f.stageHud.PerformClick();Check(f.Doc.Export().SequenceEqual(f.Doc.Rom),"Disabling Stage HUD leaves patches behind.");
            Check(!f.sram.Checked && f.sram.Enabled==f.Doc.IsProto2,"SRAM default/revision eligibility incorrect.");
            if(f.Doc.IsProto2) {
                f.sram.PerformClick();Check(f.Doc.Data.Sram && f.sram.Checked,"SRAM toggle failed.");
                string saved=f.Doc.Serialize();f.Doc.Load(saved);Check(f.Doc.Data.Sram,"SRAM project persistence failed.");
                enhancementTools.DropDownItems[0].PerformClick();Check(!f.Doc.Data.Sram && !f.sram.Checked,"SRAM undo failed.");
                enhancementTools.DropDownItems[1].PerformClick();Check(f.Doc.Data.Sram && f.sram.Checked,"SRAM redo failed.");
                byte[] sramRom=f.Doc.Export();Check(sramRom.Length==2097152 && sramRom.Skip(EnemyPatch.PatchEnd).SequenceEqual(f.Doc.Rom.Skip(EnemyPatch.PatchEnd)),"SRAM changes ROM size/header.");
                File.WriteAllBytes(Path.Combine(output,"sram.vb"),sramRom);
                f.bossRush.PerformClick();f.stageHud.PerformClick();f.Doc.SetSetting(false,0,10);
                File.WriteAllBytes(Path.Combine(output,"sram-mixed.vb"),f.Doc.Export());
                f.Doc.Load(saved);f.sram.PerformClick();Check(f.Doc.Export().SequenceEqual(f.Doc.Rom),"Disabling SRAM leaves patches behind.");
            }
            Check(!f.debugRestoration.Checked && f.debugRestoration.Enabled==f.Doc.IsProto2,"Debug default/revision eligibility incorrect.");
            if(f.Doc.IsProto2) {
                f.debugRestoration.PerformClick();Check(f.Doc.Data.DebugRestoration && f.debugRestoration.Checked,"Debug toggle failed.");
                string debugProject=f.Doc.Serialize();f.Doc.Load(debugProject);Check(f.Doc.Data.DebugRestoration,"Debug persistence failed.");
                enhancementTools.DropDownItems[0].PerformClick();Check(!f.Doc.Data.DebugRestoration && !f.debugRestoration.Checked,"Debug undo failed.");
                enhancementTools.DropDownItems[1].PerformClick();Check(f.Doc.Data.DebugRestoration && f.debugRestoration.Checked,"Debug redo failed.");
                byte[] debugRom=f.Doc.Export();Check(debugRom.Length==2097152 && debugRom.Skip(EnemyPatch.PatchEnd).SequenceEqual(f.Doc.Rom.Skip(EnemyPatch.PatchEnd)),"Debug changes size/header.");
                File.WriteAllBytes(Path.Combine(output,"debug-restoration.vb"),debugRom);
                f.sram.PerformClick();f.bossRush.PerformClick();f.stageHud.PerformClick();f.Doc.SetSetting(false,0,10);
                File.WriteAllBytes(Path.Combine(output,"debug-all-enhancements.vb"),f.Doc.Export());
                f.Doc.Load(debugProject);f.debugRestoration.PerformClick();Check(f.Doc.Export().SequenceEqual(f.Doc.Rom),"Disabling debug leaves patches behind.");
            }
            var worldSelector=Controls(f).OfType<ComboBox>().Single(c=>c.AccessibleName=="Adventure world");
            for(int w=0;w<4;w++){worldSelector.SelectedIndex=w;Check(f.Stage.World==w+1,"World selector failed.");}
            worldSelector.SelectedIndex=0;
            foreach(var combo in Controls(f).OfType<ComboBox>())Check(combo.SelectedIndex>=0 && !string.IsNullOrWhiteSpace(combo.Text),"Blank editor selector: "+combo.AccessibleName);
            Check(f.dark.Checked && f.BackColor.GetBrightness()<0.2f,"Dark mode must be the default.");
            Check(f.dark.Owner!=null,"Dark Mode must be in the toolbar.");
            var menu=Controls(f).OfType<MenuStrip>().Single();
            Check(menu.Items.Cast<ToolStripItem>().Select(i=>i.Text).SequenceEqual(new[]{"File","ROM","Tools","Game Mode","Enhancements"}),"Top-level menus differ from requested layout.");
            var fileMenu=(ToolStripMenuItem)menu.Items[0];var romMenu=(ToolStripMenuItem)menu.Items[1];var toolsMenu=(ToolStripMenuItem)menu.Items[2];
            Check(fileMenu.DropDownItems.Cast<ToolStripItem>().Select(i=>i.Text).SequenceEqual(new[]{"New Project","Open Project","Save Project","Save Project As","Exit"}),"File menu incomplete.");
            Check(romMenu.DropDownItems.Cast<ToolStripItem>().Select(i=>i.Text).SequenceEqual(new[]{"Load ROM","Export ROM","Dump Game Assets","Dump Audio Assets"}),"ROM menu incomplete.");
            Check(toolsMenu.DropDownItems.Cast<ToolStripItem>().Select(i=>i.Text).SequenceEqual(new[]{"Undo","Redo","Paint","Eraser","Dark Mode"}),"Tools menu incomplete.");
            var wind=Controls(f).OfType<CheckBox>().Single(c=>c.AccessibleName=="Wind Hazard");
            var bgm=Controls(f).OfType<ComboBox>().Single(c=>c.AccessibleName=="Stage BGM");
            var gobbler=Controls(f).OfType<CheckBox>().Single(c=>c.AccessibleName=="Gobbler Hazard");
            var worldBgm=Controls(f).OfType<CheckBox>().Single(c=>c.AccessibleName=="Apply BGM to World");
            Check(worldBgm.Text=="Apply BGM to World?" && Controls(f).OfType<Label>().Any(c=>c.Text=="Stage BGM"),"Stage BGM labels missing.");
            Check(wind.Font.Size>f.Font.Size,"Wind control was not enlarged.");

            var stageList=Controls(f).OfType<ListBox>().Single(c=>c.AccessibleName=="Adventure stages");
            string unedited=f.Doc.Serialize();
            for(int w=0;w<4;w++) {
                worldSelector.SelectedIndex=w;
                for(int st=0;st<stageList.Items.Count;st++) {
                    stageList.SelectedIndex=st;
                    Check(wind.Checked==new[]{13,15,24,25,33,37,42,45}.Contains(f.Stage.id),"Native wind checkbox incorrect.");
                    Check(bgm.SelectedIndex==0,"Stage navigation changed original music.");Check(gobbler.Checked==f.Doc.NativeGobbler(f.Stage),"Native Gobbler display wrong.");
                }
            }
            Check(f.Doc.Serialize()==unedited,"Viewing stage settings modified the project.");worldSelector.SelectedIndex=0;
            var conversion=Controls(f).OfType<CheckBox>().Single(c=>c.Text=="Regular Enemies / Clear Rules");
            var editing=Controls(f).OfType<ComboBox>().Single(c=>c.AccessibleName=="Editing layer");editing.SelectedIndex=1;
            var bonusPalette=Controls(f).OfType<ListBox>().Single(c=>c.AccessibleName=="Tile palette");
            stageList.SelectedItem=Document.Info.Single(t=>t.id==5);Check(bonusPalette.Items.Cast<int>().SequenceEqual(new[]{2,3}),"Bonus palette exposes regular enemies.");
            conversion.Checked=true;Check(bonusPalette.Items.Count==14,"Explicit bonus conversion lacks enemies.");conversion.Checked=false;Check(bonusPalette.Items.Cast<int>().SequenceEqual(new[]{2,3}),"Bonus palette not restored.");
            stageList.SelectedItem=Document.Info.Single(t=>t.id==11);Check(bonusPalette.Items.Count==0,"Post-boss bonus exposes enemies.");
            editing.SelectedIndex=0;f.Doc.Load(unedited);stageList.SelectedIndex=0;

            gobbler.Checked=true;Check(f.Doc.Setting(f.Doc.Data.Gobbler,0)==1,"Gobbler checkbox did not save.");
            toolsMenu.DropDownItems[0].PerformClick();Check(!gobbler.Checked,"Gobbler undo did not refresh checkbox.");
            toolsMenu.DropDownItems[1].PerformClick();Check(gobbler.Checked,"Gobbler redo failed.");toolsMenu.DropDownItems[0].PerformClick();
            wind.Checked=true;
            var gameTimer=Controls(f).OfType<CheckBox>().Single(c=>c.AccessibleName=="Wind Game Timing");
            var seconds=Controls(f).OfType<NumericUpDown>().Single(c=>c.AccessibleName=="Wind First Seconds");
            Check(gameTimer.Checked && !seconds.Enabled,"Game timing should disable custom input.");gameTimer.Checked=false;seconds.Value=7;
            Check(f.Doc.Timer(0,0)==175,"Seconds were not stored as 25-Hz ticks.");toolsMenu.DropDownItems[0].PerformClick();Check(seconds.Value==4,"Timer undo did not refresh the UI.");
            gameTimer.Checked=true;Check(f.Doc.Timer(0,0)==-1 && f.Doc.Timer(0,1)==-1,"Game timing did not clear both overrides.");
            f.Doc.Load(unedited);stageList.SelectedIndex=1;stageList.SelectedIndex=0;
            bgm.SelectedIndex=11;worldBgm.Checked=true;bgm.SelectedIndex=16;
            Check(Document.Info.Where(t=>t.World==1).All(t=>f.Doc.Setting(f.Doc.Data.Music,t.id)==15),"Checked world BGM scope failed.");
            Check(Document.Info.Where(t=>t.World!=1).All(t=>f.Doc.Setting(f.Doc.Data.Music,t.id)==-1),"World BGM changed another world.");
            stageList.SelectedIndex=1;Check(!worldBgm.Checked,"World BGM scope leaked across stage navigation.");
            worldBgm.Checked=false;f.Doc.Load(unedited);stageList.SelectedIndex=0;

            var click=typeof(CheckBox).GetMethod("OnClick",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            wind.Checked=false;gobbler.Checked=false;click.Invoke(wind,new object[]{EventArgs.Empty});click.Invoke(gobbler,new object[]{EventArgs.Empty});Application.DoEvents();
            Check(wind.Checked && gobbler.Checked && f.Doc.Setting(f.Doc.Data.Wind,0)==1 && f.Doc.Setting(f.Doc.Data.Gobbler,0)==1,"Hazard mouse clicks are not independent.");
            click.Invoke(wind,new object[]{EventArgs.Empty});Check(!wind.Checked && gobbler.Checked,"Disabling Wind disabled Gobbler.");
            f.Doc.Load(unedited);stageList.SelectedIndex=1;stageList.SelectedIndex=0;
            wind.Checked=true;bgm.SelectedIndex=11;
            Check(f.Doc.Setting(f.Doc.Data.Wind,0)==1 && f.Doc.Setting(f.Doc.Data.Music,0)==10,"Inline settings were not saved.");
            toolsMenu.DropDownItems[0].PerformClick();Check(bgm.SelectedIndex==0 && wind.Checked,"Undo did not refresh inline BGM.");
            toolsMenu.DropDownItems[0].PerformClick();Check(!wind.Checked,"Undo did not refresh wind.");
            toolsMenu.DropDownItems[1].PerformClick();Check(wind.Checked,"Redo wind failed.");
            toolsMenu.DropDownItems[0].PerformClick();
            Check(((ToolStripMenuItem)toolsMenu.DropDownItems[0]).ShortcutKeys==(Keys.Control|Keys.Z) && ((ToolStripMenuItem)toolsMenu.DropDownItems[1]).ShortcutKeys==(Keys.Control|Keys.Y),"History shortcuts missing.");
            toolsMenu.DropDownItems[3].PerformClick();Check(((ToolStripMenuItem)toolsMenu.DropDownItems[3]).Checked && !((ToolStripMenuItem)toolsMenu.DropDownItems[2]).Checked,"Eraser tool selection failed.");
            toolsMenu.DropDownItems[2].PerformClick();Check(((ToolStripMenuItem)toolsMenu.DropDownItems[2]).Checked,"Paint tool selection failed.");
            Check(Controls(f).OfType<CheckBox>().Select(c=>c.Text).Where(t=>new[]{"Grid","BG Layer 01","BG Layer 02","Terrain/Powerups","Enemies"}.Contains(t)).Count()==5,"Missing view layer toggle.");
            f.dark.Checked=false;Check(f.BackColor.GetBrightness()>0.5f,"Light mode switch failed.");f.dark.Checked=true;
            Check(f.canvas.ShowBackground,"Background must be visible by default.");
            f.BeginStroke();f.PaintCell(index,true);f.EndStroke();Check(f.Doc.Cells(0)[index]==0,"UI edit path failed.");
            f.Doc.Load(clean);
            var tiles=Controls(f).OfType<ListBox>().Single(c=>c.AccessibleName=="Tile palette");
            Check(!tiles.Items.Contains(18) && !tiles.Items.Contains(19),"Hidden terrain choices remain.");
            Check(tiles.Items.Contains(3) && !tiles.Items.Contains(4) && !tiles.Items.Contains(5) && !tiles.Items.Contains(6),"Spawner palette was not merged.");
            for(int i=0;i<160;i++)f.Doc.Cells(0)[i]=2;
            tiles.SelectedItem=3;
            for(int i=0;i<4;i++){f.BeginStroke();f.PaintCell(i,false);f.EndStroke();}
            Check(f.SpawnerCount==4 && f.Doc.Cells(0).Take(4).SequenceEqual(new[]{3,4,5,6}) && tiles.SelectedIndex==-1,"Spawner allocation/cap failed.");
            tiles.SelectedItem=3;Check(tiles.SelectedIndex==-1,"Full spawner option remains selectable.");
            f.BeginStroke();f.PaintCell(4,false);f.EndStroke();Check(f.Doc.Cells(0)[4]==2,"Fifth spawner painted.");
            f.Doc.Cells(0)[2]|=6<<7;
            f.BeginStroke();f.PaintCell(1,true);f.EndStroke();
            Check(f.SpawnerCount==3 && (f.Doc.Cells(0)[2]&127)==4 && f.Doc.Cells(0)[2]>>7==6 && f.Doc.Cells(0)[3]==5,"Deletion did not compact spawners while preserving enemies.");
            toolsMenu.DropDownItems[0].PerformClick();Check(f.SpawnerCount==4,"Spawner undo failed.");
            toolsMenu.DropDownItems[1].PerformClick();Check(f.SpawnerCount==3,"Spawner redo failed.");
            tiles.SelectedItem=3;f.BeginStroke();f.PaintCell(4,false);f.EndStroke();Check(f.SpawnerCount==4,"Deleted spawner slot not reusable.");
            string spawners=f.Doc.Serialize();f.Doc.Load(spawners);Check(f.SpawnerCount==4,"Spawner project roundtrip failed.");
            File.WriteAllBytes(Path.Combine(output,"spawners.vb"),f.Doc.Export());
            Check(Controls(f).OfType<CheckBox>().All(c=>c is ThemeCheckBox),"Native gray checkbox remains.");
            Application.DoEvents();using(var image=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(image,new Rectangle(0,0,f.Width,f.Height));image.Save(Path.Combine(output,"spawners-ui.png"),ImageFormat.Png);}
            f.Doc.Load(clean);
            Controls(f).OfType<ComboBox>().Single(c=>c.AccessibleName=="Editing layer").SelectedIndex=1;
            var enemyPalette=Controls(f).OfType<ListBox>().Single(c=>c.AccessibleName=="Tile palette");
            Check(enemyPalette.Items.Count==14 && enemyPalette.Items.Cast<int>().All(c=>c>=6 && c!=8 && c!=10),"Native enemy aliases remain in the palette.");
            Check(EnemyPatch.Name(f.Doc,f.Stage,2)=="Jellyfish" && EnemyPatch.Name(f.Doc,f.Stage,3)=="Jellyfish E","Native enemy naming failed.");
            for(int i=0;i<160;i++)f.Doc.Cells(0)[i]=2;
            enemyPalette.SelectedItem=20;f.BeginStroke();f.PaintCell(15,false);f.EndStroke();
            Check(f.Doc.Cells(0)[15]==2,"Large enemy placed beyond the right edge.");
            f.BeginStroke();f.PaintCell(17,false);f.EndStroke();
            Check(f.Doc.Cells(0)[17]>>7==20 && f.Doc.Cells(0)[18]==2 && f.Doc.Cells(0)[33]==2 && f.Doc.Cells(0)[34]==2,"Large placement must store one top-left anchor.");
            f.BeginStroke();f.PaintCell(34,true);f.EndStroke();Check(f.Doc.Cells(0)[17]==2,"Erasing the lower-right large sprite tile must remove its anchor.");
            var electricity=Controls(f).OfType<CheckBox>().Single(c=>c.AccessibleName=="Electric Enemy Variant");
            enemyPalette.SelectedItem=7;electricity.Checked=true;f.BeginStroke();f.PaintCell(16,false);f.EndStroke();
            Check(f.Doc.ElectricOn(f.Stage,16),"Electric painting failed.");
            toolsMenu.DropDownItems[0].PerformClick();Check(!f.Doc.ElectricOn(f.Stage,16),"Electric undo failed.");
            toolsMenu.DropDownItems[1].PerformClick();Check(f.Doc.ElectricOn(f.Stage,16),"Electric redo failed.");
            electricity.Checked=false;f.BeginStroke();f.PaintCell(16,false);f.EndStroke();Check(!f.Doc.ElectricOn(f.Stage,16),"Ordinary repaint failed.");
            enemyPalette.SelectedItem=12;Check(!electricity.Visible,"Turtle electricity checkbox remains.");f.BeginStroke();f.PaintCell(20,false);f.EndStroke();Check(f.Doc.ElectricOn(f.Stage,20),"Turtle lost default electricity.");
            var strength=Controls(f).OfType<ComboBox>().Single(c=>c.AccessibleName=="Wind Strength");
            for(int n=0;n<5;n++){strength.SelectedIndex=n;Check(f.Doc.Strength(0)==Document.Strengths[n],"Wind strength UI failed.");File.WriteAllBytes(Path.Combine(output,"strength-"+Document.Strengths[n]+".vb"),f.Doc.Export());}
            string strengthSave=f.Doc.Serialize();f.Doc.Load(strengthSave);Check(f.Doc.Strength(0)==36,"Wind strength roundtrip failed.");
            var bg0=Controls(f).OfType<ComboBox>().Single(c=>c.AccessibleName=="BG Layer 01");
            var bg1=Controls(f).OfType<ComboBox>().Single(c=>c.AccessibleName=="BG Layer 02");
            int selectedBackground=((StageInfo)bg0.Items[2]).id;bg0.SelectedIndex=2;bg1.SelectedIndex=1;
            Check(f.Doc.BackgroundChoice(0,0)==selectedBackground && f.Doc.BackgroundChoice(0,1)==-2,"Independent background settings failed.");
            for(int plane=0;plane<2;plane++){
                var box=plane==0?bg0:bg1;var keys=box.Items.OfType<StageInfo>().Select(t=>f.Art.NativeBackgroundKey(t,plane)).ToArray();
                Check(keys.Length==keys.Distinct().Count() && keys.Length<45,"Duplicate backgrounds remain.");
                File.AppendAllText(Path.Combine(output,"background-choices.txt"),"BG Layer "+(plane+1)+": "+keys.Length+" unique pictures, plus Original and None.\r\n");
            }
            string savedVisuals=f.Doc.Serialize();f.Doc.Load(savedVisuals);Check(f.Doc.BackgroundChoice(0,0)==selectedBackground && f.Doc.ElectricChoice(0,16)==0,"Visual settings roundtrip failed.");
            toolsMenu.DropDownItems[0].PerformClick();Check(bg1.SelectedIndex==0 && f.Doc.BackgroundChoice(0,0)==selectedBackground,"Background undo failed.");
            f.Doc.Cells(0)[18]=17;electricity.Checked=true;enemyPalette.SelectedItem=6;f.BeginStroke();f.PaintCell(19,false);f.EndStroke();wind.Checked=true;gobbler.Checked=true;
            ((ScrollableControl)bg1.Parent).ScrollControlIntoView(bg1);Application.DoEvents();
            using(var image=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(image,new Rectangle(0,0,f.Width,f.Height));image.Save(Path.Combine(output,"visual-settings.png"),ImageFormat.Png);}
            f.Doc.Load(clean);
            for(int t=1;t<=5;t++)f.Doc.Cells(0)[112+t*2]=2|(t<<7);
            f.canvas.Invalidate();Application.DoEvents();
            using(var image=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(image,new Rectangle(0,0,f.Width,f.Height));image.Save(Path.Combine(output,"editor.png"),ImageFormat.Png);}
        }
        var hiddenDoc=new Document(rom);
        foreach(var stage in Document.Info){if(stage.mode!=0)hiddenDoc.SetRegular(stage.id,true);for(int n=0;n<6;n++){int cell=17+n*18;hiddenDoc.Cells(stage.id)[cell]=1;hiddenDoc.SetHidden(stage.id,cell,20+n);}}
        string hiddenSave=hiddenDoc.Serialize();hiddenDoc.Load(hiddenSave);File.WriteAllBytes(Path.Combine(output,"hidden-all.vb"),hiddenDoc.Export());
        hiddenDoc.Cells(0)[17]=2;Reject(()=>hiddenDoc.Export(),"Hidden item without Breakable accepted.");hiddenDoc.Load(hiddenSave);hiddenDoc.SetHidden(0,17,33);Reject(()=>hiddenDoc.Export(),"Unsupported hidden item accepted.");
        using(var f=new Editor(rom)){
            f.Show();Application.DoEvents();var choice=Controls(f).OfType<ComboBox>().Single(c=>c.AccessibleName=="Editing layer");var palette=Controls(f).OfType<ListBox>().Single(c=>c.AccessibleName=="Tile palette");var toolsMenu=(ToolStripMenuItem)f.MainMenuStrip.Items[2];
            f.Doc.Cells(0)[17]=2|(6<<7);choice.SelectedIndex=2;palette.SelectedItem=24;f.BeginStroke();f.PaintCell(17,false);f.EndStroke();choice.SelectedIndex=0;palette.SelectedItem=1;f.BeginStroke();f.PaintCell(17,false);f.EndStroke();
            Check((f.Doc.Cells(0)[17]&127)==1 && f.Doc.HiddenChoice(0,17)==24 && f.Doc.Cells(0)[17]>>7==6,"Covering powerup lost contents or enemy.");
            toolsMenu.DropDownItems[0].PerformClick();Check((f.Doc.Cells(0)[17]&127)==24 && f.Doc.HiddenChoice(0,17)==-1,"Cover undo failed.");toolsMenu.DropDownItems[1].PerformClick();
            choice.SelectedIndex=3;Check(palette.Items.Count==6 && !palette.Items.Contains(33),"Hidden palette invalid.");palette.SelectedItem=22;f.BeginStroke();f.PaintCell(17,false);f.EndStroke();Check(f.Doc.HiddenChoice(0,17)==22,"Hidden brush failed.");
            f.Doc.Cells(0)[18]=2;f.BeginStroke();f.PaintCell(18,false);f.EndStroke();Check(f.Doc.HiddenChoice(0,18)==-1,"Hidden brush accepted Solid.");
            string savedHidden=f.Doc.Serialize();f.Doc.Load(savedHidden);Check(f.Doc.HiddenChoice(0,17)==22,"Hidden project roundtrip failed.");
            Application.DoEvents();using(var image=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(image,new Rectangle(0,0,f.Width,f.Height));image.Save(Path.Combine(output,"hidden-ui.png"),ImageFormat.Png);}
            f.BeginStroke();f.PaintCell(17,true);f.EndStroke();Check(f.Doc.HiddenChoice(0,17)==-1 && (f.Doc.Cells(0)[17]&127)==1,"Hidden eraser damaged terrain.");
            f.Doc.Load(savedHidden);choice.SelectedIndex=0;palette.SelectedItem=2;f.BeginStroke();f.PaintCell(17,false);f.EndStroke();Check(f.Doc.HiddenChoice(0,17)==-1,"Replacing Breakable retained stale content.");
        }
        var pocketDoc=new Document(rom);string pocketClean=pocketDoc.Serialize();Check(Document.PocketInfo.Length==20,"Pocket stage count.");
        Check(pocketDoc.PocketDefault(50,0)==99 && pocketDoc.PocketDefault(50,1)==11000,"Pocket defaults decoded incorrectly.");
        var v10=Document.Json.Deserialize<Project>(pocketClean);v10.Version=10;v10.ScoreStages=null;pocketDoc.Load(Document.Json.Serialize(v10));Check(pocketDoc.Rom.SequenceEqual(pocketDoc.Export()),"v10 migration changes ROM.");
        foreach(var stage in Document.PocketInfo)pocketDoc.SetPocketRules(stage.id,stage.id==50?1:stage.id==69?99:45,(stage.id-49)*10000);
        string rulesSaved=pocketDoc.Serialize();pocketDoc.Load(rulesSaved);var rulesRom=pocketDoc.Export();
        for(int at=0;at<rulesRom.Length;at++)if(rulesRom[at]!=pocketDoc.Rom[at])Check(Document.PocketInfo.Any(t=>at==d.Offset(t.id)-6 || at==d.Offset(t.id)-5 || at==d.Offset(t.id)-2 || at==d.Offset(t.id)-1),"Pocket settings changed unrelated bytes.");
        foreach(var stage in Document.PocketInfo){Check(BitConverter.ToUInt16(rulesRom,d.Offset(stage.id)-6)==pocketDoc.PocketRule(stage.id,0) && BitConverter.ToUInt16(rulesRom,d.Offset(stage.id)-2)*100==pocketDoc.PocketRule(stage.id,1),"Pocket header export failed.");}
        File.WriteAllBytes(Path.Combine(output,"pocket-rules.vb"),rulesRom);pocketDoc.SetHidden(0,17,20);pocketDoc.Cells(0)[17]=1;File.WriteAllBytes(Path.Combine(output,"pocket-rules-mixed.vb"),pocketDoc.Export());
        foreach(var invalid in new[]{new[]{0,100},new[]{100,100},new[]{99,0},new[]{99,150},new[]{99,1000000}}){pocketDoc.Load(pocketClean);pocketDoc.SetPocketRules(50,invalid[0],invalid[1]);Reject(()=>pocketDoc.Export(),"Invalid Pocket rules accepted.");}
        pocketDoc.Load(pocketClean);
        var v11=Document.Json.Deserialize<Project>(pocketClean);v11.Version=11;v11.ScoreStages=null;pocketDoc.Load(Document.Json.Serialize(v11));Check(pocketDoc.Rom.SequenceEqual(pocketDoc.Export()),"v11 migration changes ROM.");
        foreach(var stage in Document.PocketInfo){pocketDoc.SetSetting(false,stage.id,10);pocketDoc.SetBackground(stage.id,0,12);pocketDoc.SetBackground(stage.id,1,24);}
        string media=pocketDoc.Serialize();pocketDoc.Load(media);File.WriteAllBytes(Path.Combine(output,"pocket-media.vb"),pocketDoc.Export());
        pocketDoc.SetHidden(0,17,20);pocketDoc.Cells(0)[17]=1;File.WriteAllBytes(Path.Combine(output,"pocket-media-mixed.vb"),pocketDoc.Export());pocketDoc.Load(media);
        foreach(var stage in Document.PocketInfo){pocketDoc.SetSetting(false,stage.id,0);pocketDoc.SetBackground(stage.id,0,-2);pocketDoc.SetBackground(stage.id,1,-2);}File.WriteAllBytes(Path.Combine(output,"pocket-media-none.vb"),pocketDoc.Export());
        pocketDoc.Load(pocketClean);pocketDoc.SetSetting(false,50,49);File.WriteAllBytes(Path.Combine(output,"pocket-media-effect.vb"),pocketDoc.Export());pocketDoc.Load(pocketClean);
        var oldProject=Document.Json.Deserialize<Project>(pocketClean);oldProject.Version=9;oldProject.ScoreStages=null;oldProject.PocketStages=null;pocketDoc.Load(Document.Json.Serialize(oldProject));Check(pocketDoc.Cells(50).SequenceEqual(new Document(rom).Cells(50)),"Legacy Pocket migration failed.");
        foreach(var stage in Document.PocketInfo){int cell=Array.FindIndex(pocketDoc.Cells(stage.id),v=>(v&127)==29);pocketDoc.Cells(stage.id)[cell]=Document.Paint(pocketDoc.Cells(stage.id)[cell],false,30);}
        File.WriteAllBytes(Path.Combine(output,"pocket-edited.vb"),pocketDoc.Export());
        pocketDoc.SetHidden(0,17,20);pocketDoc.Cells(0)[17]=1;File.WriteAllBytes(Path.Combine(output,"pocket-mixed.vb"),pocketDoc.Export());pocketDoc.Load(pocketClean);
        for(int n=0;n<9;n++)pocketDoc.Cells(50)[n]=2|(2<<7);Reject(()=>pocketDoc.Export(),"Nine Pocket balls accepted.");pocketDoc.Load(pocketClean);pocketDoc.Cells(50)[0]=20;Reject(()=>pocketDoc.Export(),"Adventure powerup in Pocket accepted.");
        using(var f=new Editor(rom)){
            f.Show();Application.DoEvents();var menu=(ToolStripMenuItem)f.MainMenuStrip.Items[3];menu.DropDownItems[2].PerformClick();
            var editing=Controls(f).OfType<ComboBox>().Single(c=>c.AccessibleName=="Editing layer");var palette=Controls(f).OfType<ListBox>().Single(c=>c.AccessibleName=="Tile palette");var stages=Controls(f).OfType<ListBox>().Single(c=>c.AccessibleName=="Adventure stages");
            Check(stages.Items.Count==20 && f.Stage.id==50 && !editing.Visible,"Pocket mode did not switch stage/palette lists.");
            Check(!Controls(f).OfType<CheckBox>().Single(c=>c.AccessibleName=="Wind Hazard").Visible && !Controls(f).OfType<CheckBox>().Single(c=>c.AccessibleName=="Gobbler Hazard").Visible,"Adventure controls remain in Pocket.");
            Check(palette.Items.Cast<int>().SequenceEqual(new[]{256}.Concat(Document.PocketTerrain.Where(t=>t!=0))),"Pocket terrain palette mismatch.");
            var pocketBgm=Controls(f).OfType<ComboBox>().Single(c=>c.AccessibleName=="Stage BGM");var pocketBg=Controls(f).OfType<ComboBox>().Single(c=>c.AccessibleName=="BG Layer 01");var pocketBg2=Controls(f).OfType<ComboBox>().Single(c=>c.AccessibleName=="BG Layer 02");
            Check(pocketBgm.Visible && pocketBg.Visible && pocketBg2.Visible,"Pocket media controls hidden.");pocketBgm.SelectedIndex=11;pocketBg.SelectedIndex=1;pocketBg2.SelectedIndex=2;
            Check(f.Doc.Setting(f.Doc.Data.Music,50)==10 && f.Doc.BackgroundChoice(50,0)==-2 && f.Doc.BackgroundChoice(50,1)>=0,"Pocket media edits not saved.");
            var mediaTools=(ToolStripMenuItem)f.MainMenuStrip.Items[2];mediaTools.DropDownItems[0].PerformClick();Check(pocketBg2.SelectedIndex==0,"Background undo failed.");mediaTools.DropDownItems[1].PerformClick();
            stages.SelectedIndex=1;Check(pocketBgm.SelectedIndex==0 && pocketBg.SelectedIndex==0 && pocketBg2.SelectedIndex==0,"Pocket media scope leaked.");stages.SelectedIndex=0;Check(pocketBgm.SelectedIndex==11 && pocketBg.SelectedIndex==1 && pocketBg2.SelectedIndex==2,"Pocket media refresh failed.");
            menu.DropDownItems[0].PerformClick();Check(pocketBgm.Visible && pocketBg.Visible,"Adventure media lost after mode switch.");menu.DropDownItems[2].PerformClick();
            var time=Controls(f).OfType<NumericUpDown>().Single(c=>c.AccessibleName=="Pocket Time Limit");var score=Controls(f).OfType<NumericUpDown>().Single(c=>c.AccessibleName=="Pocket Score Target");
            Check(time.Visible && score.Visible && time.Value==99 && score.Value==11000,"Pocket controls/defaults missing.");time.Value=45;score.Value=25000;
            stages.SelectedIndex=1;Check(time.Value==99 && score.Value==8000 && Controls(f).OfType<Label>().Any(c=>c.Visible && c.Text=="Default: 8,000 Points"),"Stage-specific defaults wrong.");stages.SelectedIndex=0;Check(time.Value==45 && score.Value==25000,"Stage switching lost rules.");
            var ruleTools=(ToolStripMenuItem)f.MainMenuStrip.Items[2];ruleTools.DropDownItems[0].PerformClick();Check(score.Value==11000 && time.Value==45,"Pocket rules undo failed.");ruleTools.DropDownItems[1].PerformClick();Check(score.Value==25000,"Pocket rules redo failed.");
            using(var screen=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(screen,new Rectangle(0,0,f.Width,f.Height));screen.Save(Path.Combine(output,"pocket-settings-ui.png"));}
            Controls(f).OfType<Button>().Single(c=>c.Text=="Restore Stage Defaults").PerformClick();Check(time.Value==99 && score.Value==11000,"Pocket defaults restore failed.");

            int cell=Array.FindIndex(f.Doc.Cells(50),v=>(v&127)==29);palette.SelectedItem=30;f.BeginStroke();f.PaintCell(cell,false);f.EndStroke();Check((f.Doc.Cells(50)[cell]&127)==30,"Pocket terrain paint failed.");
            var tools=(ToolStripMenuItem)f.MainMenuStrip.Items[2];tools.DropDownItems[0].PerformClick();Check((f.Doc.Cells(50)[cell]&127)==29,"Pocket undo failed.");tools.DropDownItems[1].PerformClick();
            palette.SelectedItem=256;Check((int)palette.Items[0]==256,"Pocket ball palette mismatch.");int empty=Array.FindIndex(f.Doc.Cells(50),v=>(v>>7)==0);f.BeginStroke();f.PaintCell(empty,false);f.EndStroke();Check(f.Doc.Cells(50)[empty]>>7==2,"Pocket ball paint failed.");
            string beforeCapacity=f.Doc.Serialize();for(int n=0;n<160 && f.BallCount<8;n++)if((f.Doc.Cells(50)[n]>>7)==0){palette.SelectedItem=256;f.BeginStroke();f.PaintCell(n,false);f.EndStroke();}
            Check(f.BallCount==8 && palette.SelectedIndex==-1,"Full ball palette not disabled.");palette.SelectedItem=256;Check(palette.SelectedIndex==-1,"Full ball brush selectable.");
            int ballCell=Array.FindIndex(f.Doc.Cells(50),v=>(v>>7)==2);int underneath=f.Doc.Cells(50)[ballCell]&127;f.BeginStroke();f.PaintCell(ballCell,true);f.EndStroke();Check(f.BallCount==7 && (f.Doc.Cells(50)[ballCell]&127)==underneath,"Erase removed terrain beneath ball.");palette.SelectedItem=256;Check(palette.SelectedIndex==0,"Ball brush did not reenable.");f.Doc.Load(beforeCapacity);
            string project=f.Doc.Serialize();f.Doc.Load(project);menu.DropDownItems[0].PerformClick();Check(f.Stage.id<50 && editing.Items.Count==4,"Adventure switch failed.");menu.DropDownItems[2].PerformClick();Check((f.Doc.Cells(50)[cell]&127)==30 && f.Doc.Cells(50)[empty]>>7==2,"Mode switch lost Pocket edits.");
            using(var art=new Artwork(f.Doc))foreach(var stage in Document.PocketInfo)using(var map=art.Map(stage))map.Save(Path.Combine(output,"pocket-stage-"+(stage.id-49)+".png"),ImageFormat.Png);
            Application.DoEvents();using(var image=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(image,new Rectangle(0,0,f.Width,f.Height));image.Save(Path.Combine(output,"pocket-ui.png"),ImageFormat.Png);}
        }
        using(var layout=new Editor(rom)){
            layout.Show();Application.DoEvents();layout.Scale(new SizeF(1.5f,1.5f));layout.Size=new Size(1480,820);
            var modes=(ToolStripMenuItem)layout.MainMenuStrip.Items[3];
            foreach(int mode in new[]{2,0,1,2}){
                modes.DropDownItems[mode].PerformClick();Application.DoEvents();
                var bg=Controls(layout).OfType<ComboBox>().Single(c=>c.AccessibleName=="BG Layer 02");var panel=(FlowLayoutPanel)bg.Parent;panel.ScrollControlIntoView(bg);Application.DoEvents();
                Check(bg.Bottom<=panel.ClientSize.Height && bg.Top>=0,"Background dropdown clipped after scrolling at 150% scale: mode="+mode+" bg="+bg.Bounds+" panel="+panel.ClientSize+" scroll="+panel.AutoScrollPosition+" display="+panel.DisplayRectangle);
                var world=Controls(layout).OfType<CheckBox>().Single(c=>c.AccessibleName=="Apply BGM to World");
                if(mode==0){Check(world.Visible && panel.Controls.GetChildIndex(world)<panel.Controls.GetChildIndex(bg),"World BGM checkbox order wrong.");}
                else Check(!world.Visible,"World BGM option shown in Pocket.");
                using(var shot=new Bitmap(layout.Width,layout.Height)){layout.DrawToBitmap(shot,new Rectangle(0,0,layout.Width,layout.Height));shot.Save(Path.Combine(output,mode==0?"adventure-scaled-sidebar.png":mode==1?"score-scaled-sidebar.png":"pocket-scaled-sidebar.png"));}
            }
        }
        Check(Document.Digest(File.ReadAllBytes(rom))==d.SourceHash,"Input ROM changed.");
        var scoreDoc=new Document(rom);string scoreClean=scoreDoc.Serialize();
        Check(scoreDoc.Rom.SequenceEqual(scoreDoc.Export()),"Adding Score Attack changed an untouched ROM.");
        var v12=Document.Json.Deserialize<Project>(scoreClean);v12.Version=12;v12.ScoreStages=null;scoreDoc.Load(Document.Json.Serialize(v12));Check(scoreDoc.Data.ScoreStages.Count==20 && scoreDoc.Rom.SequenceEqual(scoreDoc.Export()),"v12 Score Attack migration failed.");
        foreach(var stage in Document.ScoreInfo){scoreDoc.SetScoreTarget(stage.id,12300);scoreDoc.Cells(stage.id)[100]=2;}
        scoreDoc.Load(scoreDoc.Serialize());scoreDoc.Write(Path.Combine(output,"score-native.vb"),scoreDoc.Export(),false);
        foreach(var stage in Document.ScoreInfo){
            Check(scoreDoc.ScoreTarget(stage.id)==12300,"Score target roundtrip failed.");
            for(int i=0;i<160;i++)scoreDoc.Cells(stage.id)[i]=2;
            scoreDoc.Cells(stage.id)[17]=2|(7<<7);scoreDoc.SetElectric(stage.id,17,1);
            scoreDoc.SetSetting(true,stage.id,1);scoreDoc.SetGobbler(stage.id,1);scoreDoc.SetStrength(stage.id,36);
            scoreDoc.SetTimers(stage.id,true,25,50);scoreDoc.SetTimers(stage.id,false,75,100);
            scoreDoc.SetSetting(false,stage.id,10);scoreDoc.SetBackground(stage.id,0,0);scoreDoc.SetBackground(stage.id,1,-2);
            scoreDoc.Cells(stage.id)[100]=1;scoreDoc.SetHidden(stage.id,100,24);
        }
        scoreDoc.Write(Path.Combine(output,"score-custom.vb"),scoreDoc.Export(),false);
        scoreDoc.Load(scoreClean);scoreDoc.SetScoreTarget(71,12345);Reject(()=>scoreDoc.Export(),"Non-hundred score accepted.");scoreDoc.Load(scoreClean);
        using(var scoreArt=new Artwork(scoreDoc))foreach(var stage in Document.ScoreInfo)using(var map=scoreArt.Map(stage))map.Save(Path.Combine(output,"score-stage-"+(stage.id-70)+".png"));
        using(var f=new Editor(rom)){
            f.Show();Application.DoEvents();var modes=(ToolStripMenuItem)f.MainMenuStrip.Items[3];
            Check(modes.DropDownItems[1].Text=="Score Attack!!" && modes.DropDownItems[2].Text=="Pocket and Cushion","Game Mode order incorrect.");
            modes.DropDownItems[1].PerformClick();Application.DoEvents();
            var target=Controls(f).OfType<NumericUpDown>().Single(c=>c.AccessibleName=="Score Attack Score Target");
            Check(f.Stage.id==71 && target.Visible && target.Value==3000,"Score Attack sidebar/default missing.");
            Check(Controls(f).OfType<CheckBox>().Single(c=>c.Text=="Turn Wind Hazard on?").Visible && Controls(f).OfType<CheckBox>().Single(c=>c.Text=="Turn Gobbler Hazard on?").Visible,"Score hazards missing.");
            target.Value=4500;var tools=(ToolStripMenuItem)f.MainMenuStrip.Items[2];tools.DropDownItems[0].PerformClick();Check(target.Value==3000,"Score undo failed.");tools.DropDownItems[1].PerformClick();Check(target.Value==4500,"Score redo failed.");
            modes.DropDownItems[2].PerformClick();Check(!target.Visible && f.Stage.Pocket,"Score controls leak into Pocket.");modes.DropDownItems[0].PerformClick();Check(!target.Visible,"Score controls leak into Adventure.");modes.DropDownItems[1].PerformClick();Check(target.Value==4500,"Mode switch lost Score edits.");
            using(var image=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(image,new Rectangle(0,0,f.Width,f.Height));image.Save(Path.Combine(output,"score-ui.png"));}
        }
        foreach(string file in Directory.GetFiles(output,"*.vb")){
            byte[] exported=File.ReadAllBytes(file);
            Check(exported.Length==2097152,"Export expanded the ROM: "+file);
            Check(exported.Skip(EnemyPatch.PatchEnd).SequenceEqual(d.Rom.Skip(EnemyPatch.PatchEnd)),"Cartridge header/vectors changed: "+file);
            Check(exported.Skip(0x19c774).Take(16).SequenceEqual(d.Rom.Skip(0x19c774).Take(16)),"Live pointers before padding overwritten: "+file);
        }
        var occupied=new Document(rom);occupied.SetSetting(false,0,10);occupied.Rom[EnemyPatch.PatchStart]=1;Reject(()=>occupied.Export(),"Occupied patch area accepted.");
        File.WriteAllText(Path.Combine(output,"test-results.txt"),"PASS: 45 stages across four worlds; world selector; legacy World 1 migration; isolated later-world patches and project roundtrips; byte-identical untouched export; isolated cell patch; terrain/enemy separation; project roundtrip; ROM protection; invalid project and boss rejection; weighted enemy capacity; Plasma ball counts; expanded native palette export; complete composite enemy poses; incompatible graphics rejected; all stage renders in four-color red palette; default dark theme and background; light toggle; UI painting; original ROM unchanged. Hardware validation remains pending.\r\nChanged cell: "+index+"; ROM offset: 0x"+offset.ToString("X"));
    }
}
