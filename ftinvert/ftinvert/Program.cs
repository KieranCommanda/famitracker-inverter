using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ftinvert
{
    public static class Global
    {
        public static int baseOctaveModifier = 0;
        internal static int triangleOctaveModifier = 0;
        public static List<int> notesToRaise = new List<int> {-1};
        public static List<int> notesToLower = new List<int> {-1};
        public static List<int> firstOctaves = new List<int> { -1 };
        public static List<int> octaveCorrectionStrategy = new List<int> { -1 };
        public static Dictionary<int, int> lastFromNoteInColumn = new Dictionary<int, int>();
        public static Dictionary<int, int> lastOctaveInColumn = new Dictionary<int, int>();
        public static Dictionary<int, int> lastOctaveInColumnPostInversion = new Dictionary<int, int>();

        public static bool AlterPitchBends { get; internal set; }
        public static bool ReversePitchBends { get; internal set; }
        public static bool CorrectPitchBends { get; internal set; }
        public static bool PreserveContour{ get; internal set; }

        public static int BeatLength { get; internal set; }
        public static int SkipRows { get; internal set; }
        public static int BeatsPerBar { get; internal set; }
        public static List<int> OutputBeatList = new List<int> { -1 };
    }
    class Program
    {
        
        static void Main(string[] args)
        {
            Console.WriteLine("Enter the filename without .txt:");
            var filename = Console.ReadLine();
            Console.WriteLine("Enter 1 for negative harmony, 2 for custom mapping, 3 for beat altering:");
            var mode = Console.ReadLine();
            NoteMapper noteMapper;
            NoteMapper triangleNoteMapper;
            Global.ReversePitchBends = mode == "1"; //we will revere the pitc ben if we are doing negative harmony only
            if (mode != "3")
            {
                Console.WriteLine("Correct pitch bends(y/n)?:");

                Global.CorrectPitchBends = Console.ReadLine().ToLower().Trim() == "y";
                Console.WriteLine("Alter pitch bends(y/n)?:");

                Global.AlterPitchBends = Console.ReadLine().ToLower().Trim() == "y";
            }
            if (mode == "1")
            {
                Console.WriteLine("Enter the key (e.g. C# for C sharp/D flat, C- for just C) major/minor doesn't matter:");
                var key = Console.ReadLine();
                Console.WriteLine("Preserve contour?:");
                Global.PreserveContour = Console.ReadLine().ToLower().Trim() == "y";
                if (!Global.PreserveContour)
                {
                    Console.WriteLine("Enter base octave modifier (integer):");
                    Global.baseOctaveModifier = Int32.Parse(Console.ReadLine());
                    Console.WriteLine("Enter triangle octave modifier (integer):");
                    Global.triangleOctaveModifier = Int32.Parse(Console.ReadLine());
                    Console.WriteLine("Enter a comma-separated list of notes to raise 1 octave (0-11) or -1 if none:");
                    Global.notesToRaise = Console.ReadLine().Split(',').ToList().Select(s => Int32.Parse(s.Trim())).ToList();
                    Console.WriteLine("Enter a comma-separated list of notes to lower 1 octave (0-11) or -1 if none:");
                    Global.notesToLower = Console.ReadLine().Split(',').ToList().Select(s => Int32.Parse(s.Trim())).ToList();
                }
                else
                {
                    Console.WriteLine("Enter a comma-separated list of first octaves for each column:");
                    Global.firstOctaves = Console.ReadLine().Split(',').ToList().Select(s => Int32.Parse(s.Trim())).ToList();
                    Console.WriteLine("Enter a octave correction strategy for each column (1 = average, 2 = fast, 3= slow):");
                    Global.octaveCorrectionStrategy = Console.ReadLine().Split(',').ToList().Select(s => Int32.Parse(s.Trim())).ToList();
                }
                noteMapper = new NoteMapper(key, Global.baseOctaveModifier);
                triangleNoteMapper = new NoteMapper(key, Global.triangleOctaveModifier);
            }
            else if (mode == "2")
            {

                Console.WriteLine("Enter the settings file name without .txt, or press enter for console:");
                var settingsName = Console.ReadLine();
                Console.WriteLine("Enter key to transpose the settings from C (0-11):");
                int key = Int32.Parse(Console.ReadLine());
                if (settingsName == "")
                {
                    Console.WriteLine("Create new settings file? Enter name for yes or press enter:");
                    var newSettingsName = Console.ReadLine();
                    if (newSettingsName == "")
                        noteMapper = new NoteMapper(Console.In, key);
                    else
                    {
                        var settingsFile = new FileStream(newSettingsName + ".txt", FileMode.Create);
                        var settingsWrite = new StreamWriter(settingsFile);

                        var newSettingsReader = new ReaderIntoWriter(Console.In, settingsWrite);
                        noteMapper = new NoteMapper(newSettingsReader, key);
                        settingsWrite.Close();
                    }
                }
                else
                {
                    var settingsFile = new FileStream(settingsName + ".txt", FileMode.Open);
                    var settingsRead = new StreamReader(settingsFile);

                    noteMapper = new NoteMapper(settingsRead, key);
                    settingsRead.Close();
                }
                triangleNoteMapper = noteMapper;
            }
            else
            {
                Console.WriteLine("Enter the beat length in rows:");
                int beatLength = Int32.Parse(Console.ReadLine());
                Global.BeatLength = beatLength;
                noteMapper = null;
                triangleNoteMapper = null;
                Console.WriteLine("Enter the number of rows to skip:");
                int skipRows = Int32.Parse(Console.ReadLine());
                Global.SkipRows = skipRows;
                Console.WriteLine("Enter the original number of beats per bar:");
                int beatsPerBar = Int32.Parse(Console.ReadLine());
                Global.BeatsPerBar = beatsPerBar;
                Console.WriteLine("Enter a comma-separated list of beats from the original bar, in order, to output each bar:");
                Global.OutputBeatList = Console.ReadLine().Split(',').ToList().Select(s => Int32.Parse(s.Trim())).ToList();

            }
            
            var fileRead = new FileStream(filename +".txt", FileMode.Open);
            var fileWrite = new FileStream( filename + " inverted.txt", FileMode.Create);
            var streamRead = new StreamReader(fileRead);
            var streamWrite = new StreamWriter(fileWrite);
            var currentLine = "";
            var rowNumber = 0;
            var beatNumber = 0;
            int writeBufferPosition = 0;
            int bufferReadPosition = 0;
            var readbuffer = new List<string[]>();
            var writebuffer = new List<string[]>();
            //for mode 3, read everything into the buffer 4 times first
            for (int k = 0; k < 4; k++)
            {
                while ((currentLine = streamRead.ReadLine()) != null)
                {
                    if (!currentLine.StartsWith("ROW"))
                    {
                        continue;
                    }
                    var currentLineSplit = currentLine.Split(':');
                    readbuffer.Add(currentLineSplit);
                }
                streamRead.Close();
                fileRead = new FileStream(filename + ".txt", FileMode.Open);
                streamRead = new StreamReader(fileRead);
            }
            var beatsInBar = new List<List<string[]>>();
            while ((currentLine = streamRead.ReadLine()) != null )
            {
                if (!currentLine.StartsWith("ROW"))
                {
                    streamWrite.WriteLine(currentLine);
                    continue;
                }
                
                var newLine = "";
                var currentLineSplit = currentLine.Split(':');
                if (mode != "3")
                {
                    for (int i = 0; i < currentLineSplit.Length; i++)
                    {
                        if (i != 3)
                            newLine += currentLineSplit[i].InvertNotes(noteMapper, i) + ":";
                        else
                            newLine += currentLineSplit[i].InvertNotes(triangleNoteMapper, i) + ":";
                    }
                    newLine = newLine.Substring(0, newLine.Length - 1);
                }
                else
                {
                    var changedBeat = (rowNumber % Global.BeatLength == 0);
                    if (changedBeat)
                    {
                        beatNumber++;
                        if (beatNumber > Global.OutputBeatList.Count)
                        {
                            beatNumber -= Global.OutputBeatList.Count;
                        }
                    }
                        
                    rowNumber++;
                    if (rowNumber < Global.SkipRows)
                    {
                        continue;
                    }
                    else if (rowNumber == Global.SkipRows)
                    {
                        Global.SkipRows = -1;
                        rowNumber = 0;
                        beatNumber = 1;
                        changedBeat = true;
                    }

                    if (changedBeat && beatNumber == 1) //startin a new bar, reload the beat buffer
                    {
                        beatsInBar = new List<List<string[]>>();

                        for (int i = 0; i < Global.BeatsPerBar; i++)
                        {
                            var beatRows = new List<string[]>();
                            for (int j = 0; j < Global.BeatLength; j++)
                            {
                                beatRows.Add(readbuffer.ElementAt(bufferReadPosition));
                                bufferReadPosition++;
                            }
                            beatsInBar.Add(beatRows);
                        }
                        foreach (var outputBeatNumber in Global.OutputBeatList)
                        {
                            var outputBeat = beatsInBar.ElementAt(outputBeatNumber);

                            foreach (var outputBeatRow in outputBeat)
                            {
                                writebuffer.Add(outputBeatRow);
                            }
                        }
                    }

                    readbuffer.Add(currentLineSplit);

                    var writebufferline = writebuffer.ElementAt(writeBufferPosition);
                    writeBufferPosition++;

                    var lineToWrite = writebufferline;
                    lineToWrite[0] = currentLineSplit[0];

                    var stringLineToWrite = lineToWrite.Aggregate((s,ss)=>s + ":" + ss);

                    newLine = stringLineToWrite;

                }
                streamWrite.WriteLine(newLine);
            }
            streamRead.Close();
            streamWrite.Close();
        }
    }
    public static class FTInvertHelper
    {
        public static string InvertNotes(this string cell, NoteMapper noteMapper, int column)
        {
            var returns = cell;
            for (int i = 0; i < cell.Length -2; i++)
            {
                var curSubstr = returns.Substring(i, 3);
                var newSubstr = curSubstr;
                foreach (var noteMapping in noteMapper.NoteMappings)
                {
                    if (curSubstr.Substring(0, 2) == noteMapping.FromNote)
                    {
                        
                        int parsedOctave;
                        string finalOctave;
                        //if no parse then is noise channel
                        if (Int32.TryParse(curSubstr.Substring(2, 1), out parsedOctave))
                        {
                            //log this note as the last one found in this column

                            var unmodifiedOctave = parsedOctave;
                            var notesDifference = Notes.Instance.NotesNameIndex[noteMapping.FromNote] - Notes.Instance.NotesNameIndex[noteMapping.ToNote];
                            int modifiedOctave = unmodifiedOctave;
                            var isFirstNoteInColumn = !Global.lastFromNoteInColumn.ContainsKey(column);
                            if (Global.PreserveContour)
                            {
                                if (isFirstNoteInColumn)
                                {
                                    modifiedOctave = Global.firstOctaves.ElementAt(column - 1);
                                    Global.lastFromNoteInColumn[column] = Notes.Instance.NotesNameIndex[noteMapping.FromNote];
                                    Global.lastOctaveInColumn[column] = parsedOctave;
                                    Global.lastOctaveInColumnPostInversion[column] = modifiedOctave;
                                }
                                if (!isFirstNoteInColumn)
                                {
                                    modifiedOctave = FTInvertHelper.OctaveForPreservedReversedContour(
                                        Global.lastOctaveInColumn[column],
                                        Global.lastFromNoteInColumn[column],
                                        parsedOctave,
                                        noteMapping.FromNote,
                                        Global.lastOctaveInColumnPostInversion[column],
                                        noteMapper,
                                        Global.octaveCorrectionStrategy.ElementAt(column - 1)
                                        );
                                }
                            }
                            else
                            {
                                modifiedOctave = (unmodifiedOctave + noteMapping.OctaveModifier);
                            }

                            while (modifiedOctave >= 8)
                                modifiedOctave--;
                            while (modifiedOctave <= 0)
                                modifiedOctave++;
                            
                            finalOctave = modifiedOctave + "";
                            newSubstr = noteMapping.ToNote + finalOctave;
                            if (Global.AlterPitchBends)
                            { // hack alert - replace first fx column with a pitch modifier if we are doing "custom map" for microtonal stuff
                                returns = returns.Remove(i + 9, 3);
                                returns = returns.Insert(i + 9, "P80");
                            }
                            Global.lastFromNoteInColumn[column] = Notes.Instance.NotesNameIndex[noteMapping.FromNote];
                            Global.lastOctaveInColumn[column] = parsedOctave;
                            Global.lastOctaveInColumnPostInversion[column] = modifiedOctave;
                        }
                    }
                }
                returns = returns.Remove(i, 3);
                returns = returns.Insert(i, newSubstr);
            }
            //correct a pitch shifts in this cell based on previous note found in column
            var psi = returns.IndexOf('P'); //pitch shift index

            if (psi > -1 && Global.lastFromNoteInColumn.ContainsKey(column))
            {
                var lastNote = Notes.Instance.NotesNameIndex[Global.lastFromNoteInColumn[column]];
                var noteMapping = noteMapper.NoteMappings.Single(nm => nm.FromNote == lastNote);
                if (!Global.AlterPitchBends) // new pitch shift is exclusive from PitchShiftCorrection
                {
                    var psSubString = returns.Substring(psi + 1, 2);
                    var hexPSValue = Convert.ToInt32(psSubString, 16);
                    var hexPSDelta = 128 - hexPSValue;
                    int newPSDelta;
                    if (Global.CorrectPitchBends)
                        newPSDelta = (int)Math.Round(hexPSDelta * noteMapping.PitchShiftCorrection);
                    else
                        newPSDelta = hexPSDelta;
                    if (Global.ReversePitchBends)
                        newPSDelta *= -1;
                    var newPSValue = 128 - newPSDelta;
                    while (newPSValue < 0)
                        newPSValue += 256;
                    var newPSHexString = newPSValue.ToString("X2");
                    returns = returns.Remove(psi + 1, 2);
                    returns = returns.Insert(psi + 1, newPSHexString);
                }
                else
                {
                    var diffFromC3 = (Notes.Instance.NotesNameIndex[noteMapping.ToNote] + Global.lastOctaveInColumn[column] * 12) - 36;
                    var newPSDelta = (int)Math.Round(NoteMapper.CalculateNewPitchShift(diffFromC3, noteMapping.NewPitchShiftInSemitones));
                    var newPSValue = 128 + newPSDelta;
                    while (newPSValue < 0)
                        newPSValue += 256;
                    var newPSHexString = newPSValue.ToString("X2");
                    returns = returns.Remove(psi + 1, 2);
                    returns = returns.Insert(psi + 1, newPSHexString);
                }
            }
            return returns;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="LastOctave">Last octave, pre-inversion</param>
        /// <param name="LastNote">Last note, pre-inversion</param>
        /// <param name="CurrentOctave">Current octave, pre-inversion</param>
        /// <param name="CurrentNote">Current note, pre-inversion</param>
        /// <param name="LastOctavePostInversion">Last octave, post-inversion</param>
        /// <param name="noteMapper">Note mapper for the inversion</param>
        /// <returns>The octave of the current inverted note, to preserve reversed contour after inversion</returns>
        public static int OctaveForPreservedReversedContour(int LastOctave, int LastNote, int CurrentOctave, string CurrentNote, int LastOctavePostInversion, NoteMapper noteMapper, int octaveCorrectionStrategy)
        {
 
            int lastNoteTotal = LastOctave * 12 + LastNote;
            int currentNoteTotal = CurrentOctave * 12 + Notes.Instance.NotesNameIndex[CurrentNote];
            int preInversionContour = - lastNoteTotal + currentNoteTotal;
            int currentOctavePostInversion =  CurrentOctave;
            if (octaveCorrectionStrategy == 1) // average
                currentOctavePostInversion = (int)Math.Floor((decimal)((CurrentOctave + LastOctavePostInversion) / 2) );
            if (octaveCorrectionStrategy == 2) // fast
                currentOctavePostInversion = CurrentOctave;
            if (octaveCorrectionStrategy == 3) // slow
                currentOctavePostInversion = LastOctavePostInversion;
            int postInversionContour = int.MaxValue;
            do
            {

                if (postInversionContour != int.MaxValue && !(postInversionContour == 0 && preInversionContour == 0)) // this is a sentinel value, first time we just calculate the post inversion contour
                {
                    if (postInversionContour >= 0 && preInversionContour >= 0) // pre inversion and post inversion both positive, so post inversion needs to be negative
                    {
                        currentOctavePostInversion -= 1;
                    }
                    if (postInversionContour <= 0 && preInversionContour <= 0) // pre inversion and post inversion both negative, so post inversion needs to be positive
                    {
                        currentOctavePostInversion += 1;
                    }

                }
                int lastNoteTotalPostInversion = LastOctavePostInversion * 12 + Notes.Instance.NotesNameIndex[noteMapper.NoteMappings.Single(nm => nm.FromNote == Notes.Instance.NotesNameIndex[LastNote]).ToNote];
                int currentNoteTotalPostInversion = currentOctavePostInversion * 12 + Notes.Instance.NotesNameIndex[noteMapper.NoteMappings.Single(nm=>nm.FromNote == CurrentNote).ToNote];
                postInversionContour = - lastNoteTotalPostInversion + currentNoteTotalPostInversion;
            }
            while (postInversionContour < 0 != preInversionContour > 0);

            return currentOctavePostInversion;

        }

    }
    public class NoteMapping
    {
        public int OctaveModifier;
        public string FromNote;
        public string ToNote;
        public double PitchShiftCorrection;
        public double NewPitchShiftInSemitones;

    }
    public class NoteMapper
    {
        public static double CalculatePitchShiftCorrection(int diffSemitones)
        {
            return Math.Pow(((double)18 / (double)17), -((double)diffSemitones));
        }
        public static double CalculateNewPitchShift(int diffFromC3, double diffSemitones)
        {
            return diffSemitones * 25 * CalculatePitchShiftCorrection(diffFromC3);
        }
        public List<NoteMapping> NoteMappings = new List<NoteMapping>();
        public NoteMapper(string key, int octaveModifier)
        { 
            var notes = Notes.Instance.NotesNameIndex;
            foreach (var fromNote in notes.Dict1To2.Keys)
            {
               int newOctaveModifier = octaveModifier;
                var noteMapping = new NoteMapping();
                noteMapping.FromNote = fromNote;
                // Determine new note. We are doing inversion around major/minor mediant
                int distanceFromKeyNote = notes[fromNote] - notes[key];
                int newDistanceFromKeyNote = 7 - distanceFromKeyNote;
                var toNote = (newDistanceFromKeyNote + notes[key]);
                if (toNote > 11)
                {
                    toNote %= 12;
                    newOctaveModifier += 1;
                }
                if (toNote < 0)
                {
                    toNote += 12;
                    newOctaveModifier -= 1;
                }
                var toNoteName = notes[toNote];

                if (Global.notesToLower.Contains(toNote))
                {
                    newOctaveModifier -= 1;
                }
                if (Global.notesToRaise.Contains(toNote))
                {
                    newOctaveModifier += 1;
                }
                noteMapping.PitchShiftCorrection = -1 * CalculatePitchShiftCorrection(newOctaveModifier * 12 + (toNote - notes[fromNote]));
                noteMapping.ToNote = toNoteName;
                noteMapping.OctaveModifier = newOctaveModifier;
                NoteMappings.Add(noteMapping);
            }

        }
        public NoteMapper(TextReader settings, int key)
        {
            var notes = Notes.Instance.NotesNameIndex;
            foreach (var fromNote in notes.Dict2To1.Keys)
            {
                var fromNoteValue = (fromNote + key) %12;
                var fromNoteName = notes[fromNoteValue];
                Console.WriteLine($"{notes[fromNote]} Mapping\n#########");
                Console.WriteLine($"Enter octave modifier (int):");
                var newOctaveModifier = Int32.Parse(settings.ReadLine());
                Console.WriteLine($"Enter new note (0 - 11):");
                var toNote = Int32.Parse(settings.ReadLine());
                Console.WriteLine($"Enter new pitch shift in semitones (double):");
                var newPitchShiftInSemitones = Double.Parse(settings.ReadLine());

                var noteMapping = new NoteMapping();
                
                noteMapping.FromNote = fromNoteName;
                
                var toNoteName = notes[(toNote + key) % 12];
                noteMapping.NewPitchShiftInSemitones = newPitchShiftInSemitones;
                noteMapping.PitchShiftCorrection = CalculatePitchShiftCorrection(newOctaveModifier * 12 + ((toNote + key) % 12 - fromNoteValue));
                noteMapping.ToNote = toNoteName;
                noteMapping.OctaveModifier = newOctaveModifier;
                NoteMappings.Add(noteMapping);
            }
        }
    }
    public class Notes
    {
        static Notes _instance;
        public static Notes Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;

                _instance = new Notes();
                return _instance;
            }
        }
        public DoubleDict<string, int> NotesNameIndex;
        public Notes()
        {
            NotesNameIndex = new DoubleDict<string, int>();
            NotesNameIndex[9] = "A-";
            NotesNameIndex[10] = "A#";
            NotesNameIndex[11] = "B-";
            NotesNameIndex[0] = "C-";
            NotesNameIndex[1] = "C#";
            NotesNameIndex[2] = "D-";
            NotesNameIndex[3] = "D#";
            NotesNameIndex[4] = "E-";
            NotesNameIndex[5] = "F-";
            NotesNameIndex[6] = "F#";
            NotesNameIndex[7] = "G-";
            NotesNameIndex[8] = "G#";
        }
    }
    public class DoubleDict<T1, T2>
    {
        public Dictionary<T1, T2> Dict1To2;
        public Dictionary<T2, T1> Dict2To1;

        public DoubleDict()
            {
                Dict1To2 = new Dictionary<T1, T2>();
                Dict2To1 = new Dictionary<T2, T1>();
            }
        public T1 this[T2 key]
        { get { return Dict2To1[key]; }
          set {
                Dict2To1[key] = value;
                Dict1To2[value] = key;
            } }
        public T2 this[T1 key]
        {
            get { return Dict1To2[key]; }
            set
            {
                Dict1To2[key] = value;
                Dict2To1[value] = key;
            }
        }
    }
}

public class ReaderIntoWriter : TextReader
{
    TextReader reader;
    TextWriter writer;
    public ReaderIntoWriter(TextReader reader, TextWriter writer)
    {
        this.reader = reader;
        this.writer = writer;
    }
    public override string ReadLine()
    {
        var line = reader.ReadLine();
        writer.WriteLine(line);
        return line;
    }
}