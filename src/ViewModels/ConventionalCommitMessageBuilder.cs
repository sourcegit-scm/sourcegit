﻿using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class ConventionalCommitMessageBuilder : ObservableValidator
    {
        [Required(ErrorMessage = "Type of changes can not be null")]
        public Models.ConventionalCommitType Type
        {
            get => _type;
            set => SetProperty(ref _type, value, true);
        }

        public string Scope
        {
            get => _scope;
            set => SetProperty(ref _scope, value);
        }

        [Required(ErrorMessage = "Short description can not be empty")]
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value, true);
        }

        public string Detail
        {
            get => _detail;
            set => SetProperty(ref _detail, value);
        }

        public string BreakingChanges
        {
            get => _breakingChanges;
            set => SetProperty(ref _breakingChanges, value);
        }

        public string ClosedIssue
        {
            get => _closedIssue;
            set => SetProperty(ref _closedIssue, value);
        }

        public ConventionalCommitMessageBuilder(Action<string> onApply)
        {
            _onApply = onApply;
        }

        [UnconditionalSuppressMessage("AssemblyLoadTrimming", "IL2026:RequiresUnreferencedCode")]
        public bool Apply()
        {
            if (HasErrors)
                return false;

            ValidateAllProperties();
            if (HasErrors)
                return false;

            var builder = new StringBuilder();
            builder.Append(_type.Type);

            if (!string.IsNullOrEmpty(_scope))
            {
                builder.Append("(");
                builder.Append(_scope);
                builder.Append(")");
            }

            if (!string.IsNullOrEmpty(_breakingChanges))
                builder.Append("!");
            builder.Append(": ");

            builder.Append(_description);
            builder.AppendLine().AppendLine();

            if (!string.IsNullOrEmpty(_detail))
            {
                builder.Append(_detail);
                builder.AppendLine().AppendLine();
            }

            if (!string.IsNullOrEmpty(_breakingChanges))
            {
                builder.Append("BREAKING CHANGE: ");
                builder.Append(_breakingChanges);
                builder.AppendLine().AppendLine();
            }

            if (!string.IsNullOrEmpty(_closedIssue))
            {
                builder.Append("Closed ");
                builder.Append(_closedIssue);
            }

            _onApply?.Invoke(builder.ToString());
            return true;
        }

        private Action<string> _onApply = null;
        private Models.ConventionalCommitType _type = Models.ConventionalCommitType.Supported[0];
        private string _scope = string.Empty;
        private string _description = string.Empty;
        private string _detail = string.Empty;
        private string _breakingChanges = string.Empty;
        private string _closedIssue = string.Empty;
    }
}
