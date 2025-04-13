using System.IO;

namespace SourceGit.Commands
{
    public class QueryFileChangedLines : Command
    {
        public QueryFileChangedLines(string repo, string revision1, string revision2, string filePath)
        {
            WorkingDirectory = repo;
            Context = repo;
            _repo = repo;
            _filePath = filePath;
            
            // Handle various diff scenarios
            if (string.IsNullOrEmpty(revision1) && string.IsNullOrEmpty(revision2))
            {
                // Working copy changes (unstaged)
                Args = $"diff --numstat -- \"{filePath}\"";
                _checkNewWorkingDirFile = true;
            }
            else if (string.IsNullOrEmpty(revision1) && revision2 == "--staged")
            {
                // Staged changes
                Args = $"diff --cached --numstat -- \"{filePath}\"";
                _checkNewStagedFile = true; 
            }
            else if (string.IsNullOrEmpty(revision1) || revision1 == "/dev/null")
            {
                // New file case - we'll count lines manually
                _isNewFile = true;
                _newRevision = revision2;
            }
            else
            {
                // Comparing two revisions
                Args = $"diff --numstat {revision1} {revision2} -- \"{filePath}\"";
            }
        }

        public (int, int) Result()
        {
            _addedLines = 0;
            _removedLines = 0;
            
            // Check for new files first
            if (_isNewFile || _checkNewWorkingDirFile || _checkNewStagedFile)
            {
                int lineCount = 0;
                
                if (_isNewFile && !string.IsNullOrEmpty(_newRevision))
                {
                    var stream = QueryFileContent.Run(_repo, _newRevision, _filePath);
                    using (var reader = new StreamReader(stream))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            lineCount++;
                        }
                    }
                }
                else
                {
                    var fullPath = Path.Combine(_repo, _filePath);
                    if (File.Exists(fullPath))
                    {
                        if (_checkNewWorkingDirFile || _checkNewStagedFile)
                        {
                            Exec();
                            if (_addedLines == 0 && _removedLines == 0)
                            {
                                var lines = File.ReadAllLines(fullPath);
                                lineCount = lines.Length;
                            }
                            else
                            {
                                return (_addedLines, _removedLines);
                            }
                        }
                        else
                        {
                            var lines = File.ReadAllLines(fullPath);
                            lineCount = lines.Length;
                        }
                    }
                }
                
                if (lineCount > 0)
                {
                    return (lineCount, 0);
                }
            }

            Exec();
            return (_addedLines, _removedLines);
        }

        protected override void OnReadline(string line)
        {
            var parts = line.Split('\t');
            if (parts.Length >= 2)
            {
                if (int.TryParse(parts[0], out int added))
                {
                    _addedLines = added;
                }

                if (int.TryParse(parts[1], out int removed))
                {
                    _removedLines = removed;
                }
            }
        }

        private readonly string _repo;
        private readonly string _filePath;
        private readonly bool _isNewFile = false;
        private readonly string _newRevision = null;
        private readonly bool _checkNewWorkingDirFile = false;
        private readonly bool _checkNewStagedFile = false;
        private int _addedLines;
        private int _removedLines;
    }
}
