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
        public static int baseOctaveModifier;
        internal static int triangleOctaveModifier;
        public static List<int> notesToRaise;
        public static List<int> notesToLower;
        public static Dictionary<int, int> lastFromNoteInColumn = new Dictionary<int, int>();
        public static Dictionary<int, int> lastOctaveInColumn = new Dictionary<int, int>();
    }
    class Program
    {
        
        static void Main(string[] args)
        {
            Console.WriteLine("Enter the filename without .txt:");
            var filename = Console.ReadLine();
            Console.WriteLine("Enter 1 for negative harmony, 2 for custom mapping:");
            var mode = Console.ReadLine();
            NoteMapper noteMapper;
            NoteMapper triangleNoteMapper;
            if (mode == "1")
            {
                Console.WriteLine("Enter the key (e.g. C# for C sharp/D flat, C- for just C) major/minor doesn't matter:");
                var key = Console.ReadLine();
                Console.WriteLine("Enter base octave modifier (integer):");
                Global.baseOctaveModifier = Int32.Parse(Console.ReadLine());
                Console.WriteLine("Enter triangle octave modifier (integer):");
                Global.triangleOctaveModifier = Int32.Parse(Console.ReadLine());
                Console.WriteLine("Enter a comma-separated list of notes to raise 1 octave (0-11) or -1 if none:");
                Global.notesToRaise = Console.ReadLine().Split(',').ToList().Select(s => Int32.Parse(s.Trim())).ToList();
                Console.WriteLine("Enter a comma-separated list of notes to lower 1 octave (0-11) or -1 if none:");
                Global.notesToLower = Console.ReadLine().Split(',').ToList().Select(s => Int32.Parse(s.Trim())).ToList();
                noteMapper = new NoteMapper(key, Global.baseOctaveModifier);
                triangleNoteMapper = new NoteMapper(key, Global.triangleOctaveModifier);
            }
            else
            {
                Console.WriteLine("Enter the settings file name without .txt, or press enter for console:");
                var settingsName = Console.ReadLine();
                if (settingsName == "")
                {
                    Console.WriteLine("Create new settings file? Enter name for yes or press enter:");
                    var newSettingsName = Console.ReadLine();
                    if (newSettingsName == "")
                        noteMapper = new NoteMapper(Console.In);
                    else
                    {
                        var settingsFile = new FileStream(newSettingsName + ".txt", FileMode.Create);
                        var settingsWrite = new StreamWriter(settingsFile);
                        
                        var newSettingsReader = new ReaderIntoWriter(Console.In, settingsWrite);
                        noteMapper = new NoteMapper(newSettingsReader);
                        settingsWrite.Close();
                    }
                }
                else
                {
                    var settingsFile = new FileStream(settingsName + ".txt", FileMode.Open);
                    var settingsRead = new StreamReader(settingsFile);
                    
                    noteMapper = new NoteMapper(settingsRead);
                    settingsRead.Close();
                }
                triangleNoteMapper = noteMapper;
            }
            
            var fileRead = new FileStream(filename +".txt", FileMode.Open);
            var fileWrite = new FileStream( filename + " inverted.txt", FileMode.Create);
            var streamRead = new StreamReader(fileRead);
            var streamWrite = new StreamWriter(fileWrite);
            var currentLine = "";
            while((currentLine = streamRead.ReadLine()) != null )
            {
                if (!currentLine.StartsWith("ROW"))
                {
                    streamWrite.WriteLine(currentLine);
                    continue;
                }
                
                var newLine = "";
                var currentLineSplit = currentLine.Split(':');
                for (int i = 0; i < currentLineSplit.Length; i++)
                {
                    if (i != 3)
                        newLine += currentLineSplit[i].InvertNotes(noteMapper,i) +":";
                    else
                        newLine += currentLineSplit[i].InvertNotes(triangleNoteMapper,i) + ":";
                }
                newLine = newLine.Substring(0,newLine.Length - 1);
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
                            Global.lastFromNoteInColumn[column] = Notes.Instance.NotesNameIndex[noteMapping.FromNote];
                            Global.lastOctaveInColumn[column] = parsedOctave;
                            var unmodifiedOctave = parsedOctave;

                            var modifiedOctave = (unmodifiedOctave + noteMapping.OctaveModifier);

                            if (modifiedOctave < 10 && modifiedOctave >= 0)
                                finalOctave = modifiedOctave + "";
                            else
                                finalOctave = unmodifiedOctave + "";
                            newSubstr = noteMapping.ToNote + finalOctave;
                            if (noteMapping.NewPitchShiftInSemitones != null)
                            { // hack alert - replace first fx column with a pitch modifier if we are doing "custom map" for microtonal stuff
                                returns = returns.Remove(i + 9, 3);
                                returns = returns.Insert(i + 9, "P80");
                            }
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
                if (noteMapping.NewPitchShiftInSemitones == null) // new pitch shift is exclusive from PitchShiftCorrection
                {
                    var psSubString = returns.Substring(psi + 1, 2);
                    var hexPSValue = Convert.ToInt32(psSubString, 16);
                    var hexPSDelta = 128 - hexPSValue;
                    var newPSDelta = (int)Math.Round(hexPSDelta * noteMapping.PitchShiftCorrection);
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
                }
                if (toNote < 0)
                {
                    toNote += 12;
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
        public NoteMapper(TextReader settings)
        {
            var notes = Notes.Instance.NotesNameIndex;
            foreach (var fromNote in notes.Dict1To2.Keys)
            {
                Console.WriteLine($"{fromNote} Mapping\n#########");
                Console.WriteLine($"Enter octave modifier (int):");
                var newOctaveModifier = Int32.Parse(settings.ReadLine());
                Console.WriteLine($"Enter new note (0 - 11):");
                var toNote = Int32.Parse(settings.ReadLine());
                Console.WriteLine($"Enter new pitch shift in semitones (double):");
                var newPitchShiftInSemitones = Double.Parse(settings.ReadLine());

                var noteMapping = new NoteMapping();
                
                noteMapping.FromNote = fromNote;
                
                var toNoteName = notes[toNote];
                noteMapping.NewPitchShiftInSemitones = newPitchShiftInSemitones;
                noteMapping.PitchShiftCorrection = CalculatePitchShiftCorrection(newOctaveModifier * 12 + (toNote - notes[fromNote]));
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