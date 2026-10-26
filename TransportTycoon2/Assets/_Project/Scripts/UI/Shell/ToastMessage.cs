#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

namespace TransportTycoon.UI.Shell
{
    /// <summary>
    /// How a <see cref="ToastArgument"/> is turned into text.
    /// </summary>
    public enum ToastArgumentKind
    {
        /// <summary>Used as is (already display text).</summary>
        Text = 0,

        /// <summary>A localization key resolved through the shell text cache.</summary>
        LocalizationKey = 1,

        /// <summary>A whole number formatted with the culture grouping separators.</summary>
        Amount = 2
    }

    /// <summary>
    /// One value substituted into a toast template.
    /// </summary>
    public readonly struct ToastArgument : IEquatable<ToastArgument>
    {
        private ToastArgument(ToastArgumentKind kind, string text, long amount)
        {
            Kind = kind;
            Text = text;
            Amount = amount;
        }

        /// <summary>
        /// Gets how the argument is rendered.
        /// </summary>
        public ToastArgumentKind Kind { get; }

        /// <summary>
        /// Gets the text or key (empty for amounts).
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// Gets the number for <see cref="ToastArgumentKind.Amount"/> arguments.
        /// </summary>
        public long Amount { get; }

        /// <summary>
        /// Creates an argument used as is.
        /// </summary>
        public static ToastArgument FromText(string text)
        {
            return new ToastArgument(ToastArgumentKind.Text, text ?? throw new ArgumentNullException(nameof(text)), 0L);
        }

        /// <summary>
        /// Creates an argument resolved from a localization key.
        /// </summary>
        public static ToastArgument FromKey(string key)
        {
            return new ToastArgument(
                ToastArgumentKind.LocalizationKey,
                key ?? throw new ArgumentNullException(nameof(key)),
                0L);
        }

        /// <summary>
        /// Creates a formatted whole-number argument.
        /// </summary>
        public static ToastArgument FromAmount(long amount)
        {
            return new ToastArgument(ToastArgumentKind.Amount, string.Empty, amount);
        }

        /// <inheritdoc />
        public bool Equals(ToastArgument other)
        {
            return Kind == other.Kind
                && string.Equals(Text, other.Text, StringComparison.Ordinal)
                && Amount == other.Amount;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is ToastArgument other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(Text ?? string.Empty);
                return (hash * 397) ^ Amount.GetHashCode();
            }
        }
    }

    /// <summary>
    /// A transient notification: a localization key of the template plus the values to substitute.
    /// </summary>
    public sealed class ToastMessage
    {
        private readonly ToastArgument[] _arguments;

        /// <summary>
        /// Initializes a message.
        /// </summary>
        public ToastMessage(string templateKey, IReadOnlyList<ToastArgument> arguments)
        {
            if (string.IsNullOrWhiteSpace(templateKey))
            {
                throw new ArgumentException("Template key cannot be null or whitespace.", nameof(templateKey));
            }

            if (arguments is null)
            {
                throw new ArgumentNullException(nameof(arguments));
            }

            TemplateKey = templateKey;
            _arguments = new ToastArgument[arguments.Count];
            for (int index = 0; index < arguments.Count; index++)
            {
                _arguments[index] = arguments[index];
            }
        }

        /// <summary>
        /// Gets the localization key of the template.
        /// </summary>
        public string TemplateKey { get; }

        /// <summary>
        /// Gets the values substituted into the template, in placeholder order.
        /// </summary>
        public IReadOnlyList<ToastArgument> Arguments
        {
            get
            {
                return _arguments;
            }
        }

        /// <summary>
        /// Resolves the template and its arguments into display text.
        /// </summary>
        public string Format(ShellTextCache text, CultureInfo culture)
        {
            if (text is null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            if (culture is null)
            {
                throw new ArgumentNullException(nameof(culture));
            }

            var values = new object[_arguments.Length];
            for (int index = 0; index < _arguments.Length; index++)
            {
                ToastArgument argument = _arguments[index];
                switch (argument.Kind)
                {
                    case ToastArgumentKind.LocalizationKey:
                        values[index] = text.Get(argument.Text);
                        break;
                    case ToastArgumentKind.Amount:
                        values[index] = CashFormat.FormatAmount(argument.Amount, culture);
                        break;
                    default:
                        values[index] = argument.Text;
                        break;
                }
            }

            return string.Format(culture, text.Get(TemplateKey), values);
        }
    }
}
