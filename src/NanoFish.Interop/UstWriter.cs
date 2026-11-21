using System.Text;

namespace NanoFish.Interop;

/// <summary>把编辑后的音符写回 UST（附加字段原样透传，避免丢 PBS/Envelope）。</summary>
public static class UstWriter
{
    public static void Save(string path, float tempo, IReadOnlyList<UstNoteData> notes)
    {
        var blob = new List<byte>();
        var native = new NfNoteNative[notes.Count];

        unsafe
        {
            for (var i = 0; i < notes.Count; i++)
            {
                var note = notes[i];
                var lyric = Encoding.UTF8.GetBytes(note.Lyric);
                var lyricLen = Math.Min(lyric.Length, 63);
                for (var j = 0; j < lyricLen; j++)
                {
                    native[i].Lyric[j] = lyric[j];
                }
                native[i].Lyric[lyricLen] = 0;

                var extras = Encoding.UTF8.GetBytes(note.Extras);
                native[i].StartTick = note.StartTick;
                native[i].LengthTicks = note.LengthTicks;
                native[i].NoteNum = note.NoteNum;
                native[i].Flags = note.IsRest ? 1u : 0u;
                native[i].ExtrasOff = (uint)blob.Count;
                native[i].ExtrasLen = (uint)extras.Length;
                blob.AddRange(extras);
            }

            fixed (NfNoteNative* notePtr = native)
            fixed (byte* blobPtr = blob.Count > 0 ? blob.ToArray() : Array.Empty<byte>())
            {
                var status = (NfStatus)NativeMethods.nanofish_save_ust(
                    path, tempo, notePtr, (nuint)native.Length, blobPtr, (nuint)blob.Count);
                if (status != NfStatus.Ok)
                {
                    throw new NanoFishException(status, NanoFish.LastError);
                }
            }
        }
    }
}
