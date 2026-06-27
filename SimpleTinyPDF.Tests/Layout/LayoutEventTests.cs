using System.Collections.Generic;
using Xunit;

namespace SimpleTinyPDF.Tests.Layout
{
    public class LayoutEventTests
    {
        [Fact]
        public void PageCreated_FiresForEachPage()
        {
            var layout = new PdfDocumentLayout();
            var createdPages = new List<int>();

            layout.AddEventHandler((eventType, page, ctx) =>
            {
                if (eventType == PageEventType.PageCreated)
                    createdPages.Add(ctx.PageNumber);
            });

            layout.AddParagraph("Page 1");
            layout.AddPageBreak();
            layout.AddParagraph("Page 2");
            layout.AddPageBreak();
            layout.AddParagraph("Page 3");

            var doc = layout.Generate();
            Assert.Equal(3, doc.PageCount);
            Assert.Equal(3, createdPages.Count);
            Assert.Equal(1, createdPages[0]);
            Assert.Equal(2, createdPages[1]);
            Assert.Equal(3, createdPages[2]);

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: PageCreated event fires per page");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/event-page-created");
        }

        [Fact]
        public void PageFinished_FiresForEachPage()
        {
            var layout = new PdfDocumentLayout();
            var finishedPages = new List<int>();

            layout.AddEventHandler((eventType, page, ctx) =>
            {
                if (eventType == PageEventType.PageFinished)
                    finishedPages.Add(ctx.PageNumber);
            });

            layout.AddParagraph("Page 1");
            layout.AddPageBreak();
            layout.AddParagraph("Page 2");

            var doc = layout.Generate();
            Assert.Equal(2, doc.PageCount);
            Assert.Equal(2, finishedPages.Count);
            Assert.Equal(1, finishedPages[0]);
            Assert.Equal(2, finishedPages[1]);

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: PageFinished event fires per page");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/event-page-finished");
        }

        [Fact]
        public void SectionEvents_FireAtBoundaries()
        {
            var layout = new PdfDocumentLayout();
            var events = new List<(PageEventType type, int sectionIndex)>();

            layout.AddEventHandler((eventType, page, ctx) =>
            {
                if (eventType == PageEventType.SectionStarted ||
                    eventType == PageEventType.SectionFinished)
                    events.Add((eventType, ctx.SectionIndex));
            });

            layout.AddParagraph("Section 0 content");

            layout.AddSection(new SectionOptions());
            layout.AddParagraph("Section 1 content");

            layout.AddSection(new SectionOptions());
            layout.AddParagraph("Section 2 content");

            var doc = layout.Generate();

            // Should have: Started(0), Finished(0), Started(1), Finished(1), Started(2), Finished(2)
            Assert.Equal(6, events.Count);
            Assert.Equal((PageEventType.SectionStarted, 0), events[0]);
            Assert.Equal((PageEventType.SectionFinished, 0), events[1]);
            Assert.Equal((PageEventType.SectionStarted, 1), events[2]);
            Assert.Equal((PageEventType.SectionFinished, 1), events[3]);
            Assert.Equal((PageEventType.SectionStarted, 2), events[4]);
            Assert.Equal((PageEventType.SectionFinished, 2), events[5]);

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: section events fire at boundaries");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/event-section-boundaries");
        }

        [Fact]
        public void Watermark_ViaEventHandler()
        {
            var layout = new PdfDocumentLayout();

            layout.AddEventHandler((eventType, page, ctx) =>
            {
                if (eventType == PageEventType.PageCreated)
                {
                    // Draw a "DRAFT" watermark
                    page.DrawText("DRAFT", page.Width / 2 - 50, page.Height / 2,
                        PdfFont.HelveticaBold, 48, PdfColor.Rgb(220, 220, 220));
                }
            });

            layout.AddParagraph("Page with watermark");
            layout.AddPageBreak();
            layout.AddParagraph("Another page with watermark");

            var doc = layout.Generate();
            Assert.Equal(2, doc.PageCount);

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: DRAFT watermark on every page");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/event-watermark");

            var pdfText = TestHelper.GetPdfText(bytes);
            Assert.Contains("DRAFT", pdfText);
        }

        [Fact]
        public void InterfaceHandler_Works()
        {
            var layout = new PdfDocumentLayout();
            var handler = new TestEventHandler();

            layout.AddEventHandler(handler);
            layout.AddParagraph("Test content");

            var doc = layout.Generate();

            Assert.True(handler.PageCreatedCount > 0);
            Assert.True(handler.PageFinishedCount > 0);

            foreach (var page in doc.Pages)
                TestHelper.AddDescription(page, "Verify: IPageEventHandler interface works");
            var bytes = doc.ToArray();
            TestHelper.SavePdf(bytes, "Layout/event-interface-handler");
        }

        private class TestEventHandler : IPageEventHandler
        {
            public int PageCreatedCount { get; private set; }
            public int PageFinishedCount { get; private set; }

            public void HandleEvent(PageEventType eventType, PdfPage page, PageContext context)
            {
                if (eventType == PageEventType.PageCreated) PageCreatedCount++;
                if (eventType == PageEventType.PageFinished) PageFinishedCount++;
            }
        }
    }
}
