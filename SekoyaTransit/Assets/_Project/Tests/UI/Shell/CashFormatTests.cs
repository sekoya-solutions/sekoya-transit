#nullable enable
using System;
using System.Globalization;
using NUnit.Framework;
using SekoyaTransit.UI.Shell;

namespace SekoyaTransit.UI.Tests.Shell
{
    public sealed class CashFormatTests
    {
        [Test]
        public void FormatAmount_EnglishCulture_UsesGroupingSeparators()
        {
            CultureInfo en = CultureInfo.GetCultureInfo("en-US");
            Assert.That(CashFormat.FormatAmount(1234567L, en), Is.EqualTo("1,234,567"));
        }

        [Test]
        public void FormatLabeled_TemplateAndAmount_InsertsFormattedCash()
        {
            CultureInfo en = CultureInfo.GetCultureInfo("en-US");
            string text = CashFormat.FormatLabeled("Cash: {0}", 12000L, en);
            Assert.That(text, Is.EqualTo("Cash: 12,000"));
        }

        [Test]
        public void FormatAmount_NullCulture_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => CashFormat.FormatAmount(1L, null!));
        }

        [Test]
        public void FormatLabeled_NullTemplate_Throws()
        {
            CultureInfo en = CultureInfo.GetCultureInfo("en");
            Assert.Throws<ArgumentNullException>(() => CashFormat.FormatLabeled(null!, 1L, en));
        }
    }
}
