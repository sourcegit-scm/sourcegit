using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;

using Avalonia.Input.Platform;

namespace SourceGit.ViewModels
{
    public class Apply : Popup
    {
        [CustomValidation(typeof(Apply), nameof(ValidatePatchFile))]
        public string PatchFile
        {
            get => _patchFile;
            set => SetProperty(ref _patchFile, value, true);
        }

        public bool IgnoreWhiteSpace
        {
            get => _ignoreWhiteSpace;
            set => SetProperty(ref _ignoreWhiteSpace, value);
        }

        public Models.ApplyWhiteSpaceMode SelectedWhiteSpaceMode
        {
            get;
            set;
        }

        public bool FromClipboard
        {
            get => _fromClipboard;
            set
            {
                if (SetProperty(ref _fromClipboard, value))
                    ValidateProperty(_patchFile, nameof(PatchFile));
            }
        }

        public bool ThreeWayMerge
        {
            get;
            set;
        }

        public IClipboard Clipboard
        {
            get;
            set;
        } = null;

        public Apply(Repository repo)
        {
            _repo = repo;
            SelectedWhiteSpaceMode = Models.ApplyWhiteSpaceMode.Supported[0];
        }

        public static ValidationResult ValidatePatchFile(string file, ValidationContext ctx)
        {
            if (ctx.ObjectInstance is not Apply apply)
                return new ValidationResult("Invalid object instance!!!");

            if (apply.FromClipboard || File.Exists(file))
                return ValidationResult.Success;

            return new ValidationResult($"File '{file}' can NOT be found!!!");
        }

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = "Apply patch...";

            var finalPatchFile = _patchFile;
            if (_fromClipboard)
            {
                if (Clipboard == null)
                {
                    _repo.SendNotification("Clipboard service is not available!!!", true);
                    return false;
                }

                var content = await Clipboard.TryGetTextAsync();
                if (string.IsNullOrEmpty(content) || !content.StartsWith("diff --git ", StringComparison.Ordinal))
                {
                    _repo.SendNotification("There's no valid patch content in clipboard!!!", true);
                    return false;
                }

                finalPatchFile = Path.GetTempFileName();
                File.WriteAllText(finalPatchFile, content);
            }

            var log = _repo.CreateLog("Apply Patch");
            Use(log);

            var extra = ThreeWayMerge ? "--3way" : string.Empty;
            var succ = await new Commands.Apply(_repo.FullPath, finalPatchFile, _ignoreWhiteSpace, SelectedWhiteSpaceMode.Arg, extra)
                .Use(log)
                .ExecAsync();

            log.Complete();

            if (_fromClipboard)
                File.Delete(finalPatchFile);

            return succ;
        }

        private readonly Repository _repo = null;
        private string _patchFile = string.Empty;
        private bool _fromClipboard = false;
        private bool _ignoreWhiteSpace = true;
    }
}
