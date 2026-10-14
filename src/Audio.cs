using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

sealed class AudioTrack {
    internal readonly int Choice;
    internal AudioTrack(int choice) { if(choice<1 || choice>81)throw new ArgumentOutOfRangeException("choice");Choice=choice; }
    internal bool Effect {get{return Choice>=25;}}
    internal int Id {get{return Effect?Choice-25:Choice;}}
    internal string FileName {get{return (Effect?"se":"bgm")+Id.ToString("00")+".wav";}}
    public override string ToString() {
        if(Effect)return "Sound Effect "+Id.ToString("00")+(Id==17 || Id==53?" — Control / Silent":"");
        string world=Choice==2?"World 1":Choice==10?"World 2":Choice==15?"World 3":Choice==12?"World 4":Choice==18?"Bonus Stages":null;
        return "Music "+Id.ToString("00")+(world==null?"":" — "+world);
    }
}

sealed class AudioRender {
    internal byte[] Wave;
    internal string Ending;
    internal double Seconds {get{return (Wave.Length-44)/166800.0;}}
}

sealed class AudioRenderer : IDisposable {
    [DllImport("BoundHighAudio.dll",CallingConvention=CallingConvention.Cdecl)] static extern IntPtr bhAudioCreate(byte[] rom,uint length);
    [DllImport("BoundHighAudio.dll",CallingConvention=CallingConvention.Cdecl)] static extern void bhAudioFree(IntPtr audio);
    [DllImport("BoundHighAudio.dll",CallingConvention=CallingConvention.Cdecl)] static extern int bhAudioStart(IntPtr audio,int effect,int id);
    [DllImport("BoundHighAudio.dll",CallingConvention=CallingConvention.Cdecl)] static extern int bhAudioStep(IntPtr audio,[Out] short[] samples);
    [DllImport("BoundHighAudio.dll",CallingConvention=CallingConvention.Cdecl)] static extern int bhAudioStatus(IntPtr audio);
    IntPtr handle;
    internal AudioRenderer(byte[] rom) {
        if(Document.Digest(rom)!=Document.Hash && Document.Digest(rom)!=Proto2.Hash)throw new InvalidDataException("Audio requires the supported original ROM.");
        try {handle=bhAudioCreate(rom,(uint)rom.Length);}
        catch(DllNotFoundException) {throw new InvalidOperationException("Keep BoundHighAudio.dll beside the editor executable. Extract the complete editor ZIP first.");}
        if(handle==IntPtr.Zero)throw new InvalidOperationException("The ROM's sound engine could not be initialized.");
    }
    internal AudioRender Render(AudioTrack track,CancellationToken cancel,Action<byte[]> chunk=null) {
        cancel.ThrowIfCancellationRequested();
        if(handle==IntPtr.Zero)throw new ObjectDisposedException("AudioRenderer");
        if(bhAudioStart(handle,track.Effect?1:0,track.Id)==0)throw new InvalidOperationException("Could not start this sound.");
        var samples=new short[1668];var bytes=new byte[3336];
        int limit=track.Effect?500:15000,remaining=-1,tail=0;bool fade=false;
        string ending="";
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)) {
            writer.Write(new byte[44]);
            for(int tick=0;;tick++) {
                cancel.ThrowIfCancellationRequested();
                if(bhAudioStep(handle,samples)==0)throw new InvalidOperationException("Audio rendering stopped unexpectedly.");
                if(fade)for(int frame=0;frame<834;frame++) {
                    double gain=Math.Max(0,(remaining*834.0-frame-1)/(tail*834.0));
                    samples[frame*2]=(short)(samples[frame*2]*gain);samples[frame*2+1]=(short)(samples[frame*2+1]*gain);
                }
                Buffer.BlockCopy(samples,0,bytes,0,bytes.Length);writer.Write(bytes);if(chunk!=null)chunk(bytes);
                if(remaining>=0) {if(--remaining==0)break;continue;}
                int status=bhAudioStatus(handle);
                if(status==1) {ending="Natural End";remaining=tail=8;}
                else if(status==2 || tick+1>=limit) {
                    ending=status==2?"Two Loops + Fade":track.Effect?"Sustained Effect: 10 Seconds + Fade":"Safety Limit: 300 Seconds + Fade";
                    fade=true;remaining=tail=100;
                }
            }
            int length=checked((int)stream.Length-44);stream.Position=0;
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));writer.Write(length+36);writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);writer.Write((short)1);writer.Write((short)2);writer.Write(41700);writer.Write(166800);writer.Write((short)4);writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write(length);
            return new AudioRender {Wave=stream.ToArray(),Ending=ending};
        }
    }
    public void Dispose() {if(handle!=IntPtr.Zero){bhAudioFree(handle);handle=IntPtr.Zero;}}

    internal static void Dump(byte[] rom,string folder,CancellationToken cancel,IProgress<string> progress) {
        Directory.CreateDirectory(folder);
        // CreateNew prevents overwriting unrelated user files, even in command-line use.
        using(var index=new StreamWriter(new FileStream(Path.Combine(folder,"INDEX.txt"),FileMode.CreateNew),Encoding.UTF8))
        using(var renderer=new AudioRenderer(rom)) {
            index.WriteLine("Bound High! ROM-derived audio — stereo PCM WAV, 16-bit, 41,700 Hz.");
            index.WriteLine("Source SHA-1: "+Document.Digest(rom));
            index.WriteLine("Music: two native sequence loops plus a two-second fade, or its natural end.");
            index.WriteLine("Sustained effects are limited to ten seconds plus fade. Control-only entries may be silent.");
            index.WriteLine("Generated with the bundled emulator; no audio assets or ROM are distributed with the editor.");
            index.WriteLine("If cancelled, completed WAVs remain; an incomplete export has no COMPLETE line.");
            index.Flush();
            for(int choice=1;choice<=81;choice++) {
                cancel.ThrowIfCancellationRequested();var track=new AudioTrack(choice);
                if(progress!=null)progress.Report("Rendering "+track+" ("+choice+" / 81)…");
                var result=renderer.Render(track,cancel);
                using(var file=new FileStream(Path.Combine(folder,track.FileName),FileMode.CreateNew))file.Write(result.Wave,0,result.Wave.Length);
                index.WriteLine(track.FileName+" | "+track+" | "+result.Seconds.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+" s | "+result.Ending);index.Flush();
            }
            index.WriteLine("COMPLETE: 24 music entries and 57 sound effects.");
        }
    }
}

sealed class AudioDialog : Form {
    readonly Document document;
    readonly byte[] rom;
    readonly ListBox tracks=new ListBox {Dock=DockStyle.Fill,AccessibleName="Music and sound effects"};
    readonly Button play=new Button {Text="Play",AutoSize=true}, stop=new Button {Text="Stop",AutoSize=true}, save=new Button {Text="Save WAV",AutoSize=true}, dump=new Button {Text="Dump All WAVs",AutoSize=true};
    readonly Label message=new Label {Dock=DockStyle.Fill,AutoSize=true,Text="Choose a sound to preview or export."};
    CancellationTokenSource cancellation;
    AudioRender cached;
    int cachedChoice;
    bool busy,closing;
    internal AudioDialog(Document source,int choice) {
        document=source;rom=source.Rom;Text="Audio From ROM";Width=590;Height=560;MinimumSize=new Size(520,450);StartPosition=FormStartPosition.CenterParent;Font=new Font("Segoe UI",10);
        var panel=new TableLayoutPanel {Dock=DockStyle.Fill,RowCount=4,ColumnCount=1,Padding=new Padding(12)};
        panel.RowStyles.Add(new RowStyle(SizeType.Percent,100));panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));panel.RowStyles.Add(new RowStyle(SizeType.Absolute,62));panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var buttons=new FlowLayoutPanel {AutoSize=true,Dock=DockStyle.Fill};buttons.Controls.AddRange(new Control[]{play,stop,save,dump});
        panel.Controls.Add(tracks,0,0);panel.Controls.Add(buttons,0,1);panel.Controls.Add(message,0,2);
        panel.Controls.Add(new Label {AutoSize=true,MaximumSize=new Size(540,0),Text="Music: Two Loops + Fade. Sustained Effects: 10 s + Fade.\nAudio is generated from your ROM. Stop also cancels rendering."},0,3);Controls.Add(panel);
        for(int i=1;i<=81;i++)tracks.Items.Add(new AudioTrack(i));tracks.SelectedIndex=Math.Max(0,Math.Min(80,choice-1));
        play.Click+=(s,e)=>Start(false,false);save.Click+=(s,e)=>Start(true,false);dump.Click+=(s,e)=>Start(false,true);
        stop.Click+=(s,e)=>{StopAudio();if(cancellation!=null)cancellation.Cancel();message.Text=busy?"Cancelling…":"Stopped.";};
        tracks.SelectedIndexChanged+=(s,e)=>{StopAudio();if(cancellation!=null)cancellation.Cancel();};
        FormClosing+=(s,e)=>{StopAudio();if(busy){e.Cancel=true;closing=true;cancellation.Cancel();}};
    }
    internal void DumpAll() {Start(false,true);}
    async void Start(bool export,bool all) {
        if(busy)return;StopAudio();var track=(AudioTrack)tracks.SelectedItem;string destination=null;
        if(all)using(var folder=new FolderBrowserDialog {Description="Choose where to create a new folder containing all music and sound effects."}) {
            if(folder.ShowDialog(this)!=DialogResult.OK)return;
            destination=Path.Combine(folder.SelectedPath,"BoundHigh-Audio-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6));
        }
        else if(export)using(var file=new SaveFileDialog {Filter="WAV Audio|*.wav",FileName=track.FileName,OverwritePrompt=true}) {if(file.ShowDialog(this)!=DialogResult.OK)return;destination=file.FileName;}
        busy=true;play.Enabled=save.Enabled=dump.Enabled=false;var operation=new CancellationTokenSource();cancellation=operation;var token=operation.Token;
        message.Text="Preparing "+(all?"Audio Export":track.ToString())+"…";
        try {
            if(all) {
                var progress=new Progress<string>(value=>{if(!IsDisposed)message.Text=value;});
                await Task.Run(()=>AudioRenderer.Dump(rom,destination,token,progress));
                message.Text="Saved All 81 WAVs To:\n"+destination;
            } else if(!export) {
                var progress=new Progress<string>(value=>{if(!IsDisposed && !token.IsCancellationRequested)message.Text=value;});
                cached=await Task.Run(()=>AudioStream.Play(rom,track,token,()=>((IProgress<string>)progress).Report("Playing "+track)));cachedChoice=track.Choice;
            } else {
                if(cached==null || cachedChoice!=track.Choice) {
                    cached=null;
                    cached=await Task.Run(()=>{using(var renderer=new AudioRenderer(rom))return renderer.Render(track,token);});cachedChoice=track.Choice;
                }
                token.ThrowIfCancellationRequested();
                document.Write(destination,cached.Wave,".wav");message.Text="Saved "+track.FileName+" — "+cached.Ending;
            }
        } catch(OperationCanceledException) {message.Text=all?"Cancelled. Completed WAVs remain in:\n"+destination:"Stopped.";}
        catch(Exception ex) {StopAudio();message.Text="Audio Failed.";MessageBox.Show(this,ex.Message,"Audio",MessageBoxButtons.OK,MessageBoxIcon.Error);}
        finally {operation.Dispose();cancellation=null;busy=false;play.Enabled=save.Enabled=dump.Enabled=true;if(closing)Close();}
    }
    void StopAudio() {if(cancellation!=null)cancellation.Cancel();}
    protected override void Dispose(bool disposing) {if(disposing)StopAudio();base.Dispose(disposing);}
}
// Bounded native Windows PCM queue. All waveOut calls and buffers belong to one worker.
sealed class AudioStream : IDisposable {
    [StructLayout(LayoutKind.Sequential,Pack=2)] struct Format {public ushort Tag,Channels;public uint Rate,Bytes;public ushort Align,Bits,Extra;}
    [StructLayout(LayoutKind.Sequential)] struct Header {public IntPtr Data;public uint Length,Recorded;public UIntPtr User;public uint Flags,Loops;public IntPtr Next;public UIntPtr Reserved;}
    [DllImport("winmm.dll")] static extern uint waveOutOpen(out IntPtr h,uint device,ref Format f,IntPtr cb,IntPtr instance,uint flags);
    [DllImport("winmm.dll")] static extern uint waveOutPrepareHeader(IntPtr h,IntPtr p,uint size);
    [DllImport("winmm.dll")] static extern uint waveOutWrite(IntPtr h,IntPtr p,uint size);
    [DllImport("winmm.dll")] static extern uint waveOutUnprepareHeader(IntPtr h,IntPtr p,uint size);
    [DllImport("winmm.dll")] static extern uint waveOutReset(IntPtr h);
    [DllImport("winmm.dll")] static extern uint waveOutClose(IntPtr h);
    [DllImport("winmm.dll")] static extern uint waveOutPause(IntPtr h);
    [DllImport("winmm.dll")] static extern uint waveOutRestart(IntPtr h);
    readonly System.Collections.Generic.Queue<IntPtr> pending=new System.Collections.Generic.Queue<IntPtr>();
    readonly CancellationToken token;readonly Action started;IntPtr device;bool playing;
    static readonly uint headerSize=(uint)Marshal.SizeOf(typeof(Header));
    internal AudioStream(CancellationToken cancellation,Action onStart){token=cancellation;started=onStart;var f=new Format {Tag=1,Channels=2,Rate=41700,Bytes=166800,Align=4,Bits=16};Check(waveOutOpen(out device,uint.MaxValue,ref f,IntPtr.Zero,IntPtr.Zero,0));try{Check(waveOutPause(device));}catch{Dispose();throw;}}
    static void Check(uint code){if(code!=0)throw new IOException("Windows audio error "+code+".");}
    void Reap(){while(pending.Count>0){var p=pending.Peek();var h=(Header)Marshal.PtrToStructure(p,typeof(Header));if((h.Flags&1)==0)break;Check(waveOutUnprepareHeader(device,p,headerSize));pending.Dequeue();Marshal.FreeHGlobal(h.Data);Marshal.FreeHGlobal(p);}}
    void Start(){if(playing)return;Check(waveOutRestart(device));playing=true;if(started!=null)started();}
    internal void Write(byte[] bytes){token.ThrowIfCancellationRequested();Reap();while(pending.Count>=10){token.WaitHandle.WaitOne(5);token.ThrowIfCancellationRequested();Reap();}var h=new Header {Data=Marshal.AllocHGlobal(bytes.Length),Length=(uint)bytes.Length};Marshal.Copy(bytes,0,h.Data,bytes.Length);IntPtr p=Marshal.AllocHGlobal((int)headerSize);Marshal.StructureToPtr(h,p,false);uint code=waveOutPrepareHeader(device,p,headerSize);if(code!=0){Marshal.FreeHGlobal(h.Data);Marshal.FreeHGlobal(p);Check(code);}pending.Enqueue(p);Check(waveOutWrite(device,p,headerSize));if(pending.Count>=4)Start();}
    internal void Drain(){Start();while(pending.Count>0){token.ThrowIfCancellationRequested();Reap();if(pending.Count>0)token.WaitHandle.WaitOne(5);}}
    public void Dispose(){if(device==IntPtr.Zero)return;waveOutReset(device);while(pending.Count>0){var p=pending.Dequeue();var h=(Header)Marshal.PtrToStructure(p,typeof(Header));waveOutUnprepareHeader(device,p,headerSize);Marshal.FreeHGlobal(h.Data);Marshal.FreeHGlobal(p);}waveOutClose(device);device=IntPtr.Zero;}
    internal static AudioRender Play(byte[] rom,AudioTrack track,CancellationToken token,Action started){using(var renderer=new AudioRenderer(rom))using(var output=new AudioStream(token,started)){var result=renderer.Render(track,token,output.Write);output.Drain();return result;}}
}
