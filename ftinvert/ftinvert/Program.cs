using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ftinvert
{
    class Program
    {
        static void Main(string[] args)
        {
            var fileRead = new FileStream("ducktales main.txt", FileMode.Open);
            var fileWrite = new FileStream("ducktales main inverted.txt", FileMode.Create);
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

                var newLine = currentLine.InvertNotes();
                streamWrite.WriteLine(newLine);
            }
            streamRead.Close();
            streamWrite.Close();
        }
    }
    public static class FTInvertHelper
    {
        public static string InvertNotes(this string row)
        {
            var noteMapper = new NoteMapper("E-");
            var returns = row;
            for (int i = 0; i < row.Length -2; i++)
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
                            var unmodifiedOctave = parsedOctave;
                            var modifiedOctave = (unmodifiedOctave + noteMapping.OctaveModifier);

                            if (modifiedOctave < 10 && modifiedOctave >= 0)
                                finalOctave = modifiedOctave + "";
                            else
                                finalOctave = unmodifiedOctave + "";
                            newSubstr = noteMapping.ToNote + finalOctave;
                        }
                        
                    }
                }
                returns = returns.Remove(i, 3);
                returns = returns.Insert(i, newSubstr);
            }
            return returns;
        }

    }
    public class NoteMapping
    {
        public int OctaveModifier;
        public string FromNote;
        public string ToNote;
    }
    public class NoteMapper
    {
        public List<NoteMapping> NoteMappings = new List<NoteMapping>();
        public NoteMapper(string key)
        {
            var notes = Notes.Instance.NotesNameIndex;
            foreach (var fromNote in notes.Dict1To2.Keys)
            {
                var noteMapping = new NoteMapping();
                noteMapping.FromNote = fromNote;
                // Determine new note. We are doing inversion around major/minor mediant
                int distanceFromKeyNote = notes[fromNote] - notes[key];
                if (distanceFromKeyNote < 0)
                    distanceFromKeyNote = distanceFromKeyNote + 12;
                int newDistanceFromKeyNote = 7 - distanceFromKeyNote;
                if (newDistanceFromKeyNote < 0)
                    newDistanceFromKeyNote += 12;
                var toNote = ((newDistanceFromKeyNote + notes[key] -1) % 12) + 1;
                var toNoteName = notes[toNote];

                //Determine new octave. We will do -1 octave if difference is greater than or equal to +4;
                int octaveModifier = 0;
                if (toNote - notes[fromNote] >= 4)
                {
                    octaveModifier = -1;
                }
                noteMapping.ToNote = toNoteName;
                noteMapping.OctaveModifier = octaveModifier;
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
            NotesNameIndex[1] = "A-";
            NotesNameIndex[2] = "A#";
            NotesNameIndex[3] = "B-";
            NotesNameIndex[4] = "C-";
            NotesNameIndex[5] = "C#";
            NotesNameIndex[6] = "D-";
            NotesNameIndex[7] = "D#";
            NotesNameIndex[8] = "E-";
            NotesNameIndex[9] = "F-";
            NotesNameIndex[10] = "F#";
            NotesNameIndex[11] = "G-";
            NotesNameIndex[12] = "G#";

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