using System;
using System.Linq;
using System.Threading.Tasks;
using betten.Model;
using OfficeOpenXml;

namespace betten.Utils
{
    public class ExcelExporter
    {
        private BettenContext dbContext;
        private int eventId;

        public ExcelExporter(BettenContext dbContext, int eventId)
        {
            this.dbContext = dbContext;
            this.eventId = eventId;
            Console.WriteLine(eventId);
        }

        public async Task<byte[]> Export()
        {
            return await Task.Run(() =>
            {
                var evt = dbContext.Events.First(e => e.Id == eventId);
                using (var pkg = new ExcelPackage())
                {
                    var sheetName = $"{evt.Title} {evt.Date}";
                    var sheet = pkg.Workbook.Worksheets.Add(sheetName);

                    sheet.Cells["A1"].Value = "Unfallhilfstellendokumentation";

                    sheet.Cells["A2"].Value = "Datum:";
                    sheet.Cells["B2"].Value = evt.Date;
                    sheet.Cells["C2"].Value = "Einsatzort:";
                    sheet.Cells["D2"].Value = evt.Title;

                    sheet.Cells["A4"].Value = "Lfd.Nr.";
                    sheet.Cells["B4"].Value = "Versorgungsbeginn";
                    sheet.Cells["C4"].Value = "Pat.Name, Vorname";
                    sheet.Cells["D4"].Value = "chirurgisch";
                    sheet.Cells["E4"].Value = "internistisch";
                    sheet.Cells["F4"].Value = "sonstige";
                    sheet.Cells["G4"].Value = "Name Einsatzkraft/Notarzt";
                    sheet.Cells["H4"].Value = "Versorgungsende/Übergabe";
                    sheet.Cells["I4"].Value = "Protokoll erledigt";
                    sheet.Cells["J4"].Value = "entlassen";
                    sheet.Cells["K4"].Value = "Kliniktransport";
                    sheet.Cells["L4"].Value = "Bemerkungen";

                    var row = 5;
                    var patients = dbContext.Patients.Where(p => p.EventId == eventId).ToArray();
                    foreach (var patient in patients)
                    {
                        sheet.Cells["A" + row].Value = patient.PatientNumber;
                        sheet.Cells["B" + row].Value = patient.Admission;
                        sheet.Cells["C" + row].Value = $"{patient.Name}, {patient.FirstName}";
                        switch (patient.Discipline)
                        {
                            case "Chirurgisch":
                                sheet.Cells["D" + row].Value = "X";
                                break;
                            case "Internistisch":
                                sheet.Cells["E" + row].Value = "X";
                                break;
                            default:
                                sheet.Cells["F" + row].Value = "X";
                                break;
                        }
                        sheet.Cells["G" + row].Value = evt.Physician;
                        sheet.Cells["H" + row].Value = patient.Discharge;
                        if (patient.Transported)
                        {
                            sheet.Cells["K" + row].Value = "X";
                        }
                        sheet.Cells["L" + row].Value = patient.Comment;
                        row++;
                    }

                    return pkg.GetAsByteArray();
                }
            });
        }
    }
}