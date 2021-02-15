using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Easyweb.Core;
using Easyweb.Core.Attributes;
using Easyweb.Core.Templates;
using Easyweb.Core.Extensions;
using Albatross.Infrastructure;
using Albatross.Infrastructure.Extensions;
using Albatross.Infrastructure.Services;
using Easyweb.Core.Entities;
using System.Globalization;
using Microsoft.AspNetCore.Http;
using System.Net.Mail;
using Easyweb.Core.Utility;

namespace Albatross.Project
{
    /// <summary>
    /// --- I promise by my life to change this comment to a useful
    ///             comment on what the purpose of my component is! ---
    /// </summary>
    [RendersFor("ReferenceType", "BookingCalendar")]
    public class BookingCalendarComponent : ComponentBase<BookingCalendarTypeOptions>
    {

        public string Testing { get; set; } = "En test url";
        public string ApiUrl { get; set; } = "test";


        // This is used througout to know what dates we start with when navigating back and forth in the calendar.
        private List<DateTime> WeekControl { get; set; } = new List<DateTime>();


        #region Database models

        public Easyweb.Core.Entities.Calendar Calendar { get; set; }
        private List<CalendarBooking> CalendarBookings { get; set; } = new List<CalendarBooking>();
        private List<CalendarDay> CalendarDays { get; set; } = new List<CalendarDay>();
        private List<CalendarTimeSpan> CalendarTimeSpans { get; set; } = new List<CalendarTimeSpan>();

        #endregion


        #region Models for the view

        private List<CalendarTimeSpanModel> CalendarTimeSpanModels { get; set; } = new List<CalendarTimeSpanModel>();
        public string CalendarLabel { get; set; }
        public List<CalendarViewModel> CalendarViewModel { get; set; } = new List<CalendarViewModel>();
        public DateTime FirstDateToDisplay => CalendarViewModel.OrderBy(x => x.WeekDay).FirstOrDefault().WeekDay;
        public DateTime LastDateToDisplay => CalendarViewModel.OrderByDescending(x => x.WeekDay).FirstOrDefault().WeekDay;
        public DateTime FirstMonday => DateTime.Now.StartOfWeek(DayOfWeek.Monday);
        public List<DateTime> UnavalibleDates { get; set; } = new List<DateTime>();


        public bool ShowUnbook { get; set; } = false;
        #endregion


        /// <summary>
        /// Called when view and templates have been loaded, and starts populating themselves with data
        /// </summary>
        protected override IRenderable Contextualize()
        {
            // In the future we might have different pre-designed templates.
            //
            //if (this.Options.CalendarTemplate == 1)
            //{
            //}

            var navQuery = HttpContext.Request.Query["nav"].FirstOrDefault();
            if (navQuery == "next" || navQuery == "prev")
            {
                TemplateResult();
            }

            var unBookHashQuery = HttpContext.Request.Query["unbook"].FirstOrDefault();
            var bookingIdQuery = HttpContext.Request.Query["bookingid"].FirstOrDefault();
            var postRequest = HttpContext.Request.Method == "post";
            if (!String.IsNullOrEmpty(unBookHashQuery) && !String.IsNullOrEmpty(bookingIdQuery))
            {
                if (!postRequest)
                {
                    ShowUnbook = true;
                }
            }

            InitCalendar();

            return base.Contextualize();
        }

        private void InitCalendar()
        {
            // If week is not yet set, we need to populate it.
            if (WeekControl.Count <= 0)
                WeekControl = InitWeek();

            // If this is the initial request we need to get data.
            if (Calendar == null)
            {
                Calendar = DataSource.Set<Easyweb.Core.Entities.Calendar>().Where(x => x.Id == this.WebContent.RefObjectId).FirstOrDefault();
                if (Calendar.Id <= 0)
                {
                    // Hitta ingen calender
                }

                CalendarLabel = Calendar.Label;

                CalendarBookings = DataSource.Set<CalendarBooking>().Where(x => x.CalendarId == Calendar.Id && x.Date > DateTime.Now.AddDays(-1)).ToList();

                CalendarDays = DataSource.Set<CalendarDay>().Where(x => x.CalendarId == Calendar.Id).ToList();

                List<int> caledarDaysIds = CalendarDays.Select(x => x.Id).ToList();
                CalendarTimeSpans = DataSource.Set<CalendarTimeSpan>()
                    .Where(x => caledarDaysIds.Contains(x.CalendarDayId))
                    .ToList();


                CalendarTimeSpanModels = AutoMapTimeSpans(CalendarTimeSpans);

                PopulateCalendar(CalendarDays, CalendarTimeSpanModels, WeekControl, CalendarBookings);
            }
        }

        /// <summary>
        /// Some automapping because we want to display decimals as real timestamps, etc 7.5 as 07:00.
        /// </summary>
        /// <param name="calendarTimeSpans"></param>
        /// <returns></returns>
        private List<CalendarTimeSpanModel> AutoMapTimeSpans(List<CalendarTimeSpan> calendarTimeSpans)
        {
            var model = new List<CalendarTimeSpanModel>();
            foreach (var item in calendarTimeSpans)
            {
                var addMe = new CalendarTimeSpanModel
                {
                    Id = item.Id,
                    UnionId = item.UnionId,
                    CalendarDayId = item.CalendarDayId,
                    EndHour = item.EndHour,
                    StartHour = item.StartHour
                };
                model.Add(addMe);
            }
            return model;
        }

        /// <summary>
        /// Populate the model we are going to use for the calendar, we map each day of a week with the 
        /// corresponding CalendayDay and TimeSpans for that calendar day.
        /// This also acts as the rendering model, just pass it the data and it does the mapping job.
        /// </summary>
        /// <param name="calendarDays"></param>
        /// <param name="calendarTimeSpans"></param>
        private void PopulateCalendar(
            List<CalendarDay> calendarDays,
            List<CalendarTimeSpanModel> calendarTimeSpans,
            List<DateTime> weekDays,
            List<CalendarBooking> bookings)
        {
            CalendarViewModel = new List<CalendarViewModel>();
            foreach (var day in weekDays)
            {
                var weekDayTimeSpanModel = new CalendarViewModel
                {
                    WeekDay = day,
                };

                var calendarDay = calendarDays.FirstOrDefault(x => x.DayOfWeek == day.DayOfWeek);
                weekDayTimeSpanModel.CalendarDay = calendarDay;

                foreach (var timespan in calendarTimeSpans.Where(x => x.CalendarDayId == weekDayTimeSpanModel.CalendarDay.Id))
                {
                    if (timespan.CalendarDayId == weekDayTimeSpanModel.CalendarDay.Id)
                    {
                        weekDayTimeSpanModel.CalendarTimeSpans.Add(timespan);
                    }

                    // Check if a booking takes places during this time, if yes, check if it matches our timespan.
                    var bookedDate = bookings.FirstOrDefault(x => x.Date.Date == day.Date && x.CalendarTimeSpanId == timespan.Id);
                    if (bookedDate != null)
                        timespan.Unavalible = true;
                }

                weekDayTimeSpanModel.CalendarTimeSpans = weekDayTimeSpanModel.CalendarTimeSpans.OrderBy(x => x.StartHour).ToList();

                CalendarViewModel.Add(weekDayTimeSpanModel);
            }

            UnavalibleDates = bookings.Where(x => x.AllDay == true).Select(x => x.Date).ToList();
        }

        private List<DateTime> InitWeek()
        {
            var weekDays = new List<DateTime>();

            var today = DateTime.Now.Date;
            // We use StartOfWeek extension because the calendar should always start on a monday.
            var firstMonday = today.StartOfWeek(DayOfWeek.Monday);

            weekDays.Add(firstMonday);
            for (int i = 1; i < 7; i++)
            {
                var addDate = firstMonday.AddDays(i);
                weekDays.Add(addDate);
            }
            return weekDays;
        }

        private List<DateTime> NextWeek(DateTime lastDate)
        {
            var weekDays = new List<DateTime>();

            for (int i = 1; i < 8; i++)
            {
                var addDate = lastDate.Date.AddDays(i);
                weekDays.Add(addDate);
            }

            return weekDays;
        }

        private List<DateTime> PrevWeek(DateTime firstDate)
        {
            var weekDays = new List<DateTime>();

            for (int i = 7; i > 0; i--)
            {
                var addDate = firstDate.Date.AddDays(-i);
                weekDays.Add(addDate);
            }

            return weekDays;
        }

        #region - Possible api result -
        /// <summary>
        /// Return possible api-result. /api/[url]?key=[key]
        /// Json Example: return new JsonResult(myList);
        /// </summary>
        public override IActionResult ApiResult()
        {
            return base.ApiResult();
        }
        #endregion

        #region - Possible ajax request result -
        /// <summary>
        /// Return possible template-result (html) if requested directly. /template/[url]?key=[key]
        /// </summary>
        public override IActionResult TemplateResult()
        {
            var navQuery = HttpContext.Request.Query["nav"].FirstOrDefault();
            var dateQuery = HttpContext.Request.Query["date"].FirstOrDefault();
            DateTime date = DateTime.Now;
            if (dateQuery != null)
            {
                date = DateTime.Parse(dateQuery);
            }

            // /template/kontakt?key=BookingCalendar&nav=next
            if (navQuery == "next")
            {
                WeekControl = NextWeek(date);
            }
            // /template/kontakt?key=BookingCalendar&nav=prev
            if (navQuery == "prev")
            {
                WeekControl = PrevWeek(date);
            }

            PopulateCalendar(CalendarDays, CalendarTimeSpanModels, WeekControl, CalendarBookings);

            return base.TemplateResult();
        }

        /// <summary>
        /// Create a calendar booking form the formcollection.
        /// </summary>
        /// <param name="formCollection"></param>
        /// <returns></returns>
        private CalendarBooking CreateBooking(IFormCollection formCollection)
        {
            var name = formCollection["name"].ToString();
            var date = DateTime.Parse(formCollection["date"]);
            var timespanid = Int32.Parse(formCollection["timespanid"]);
            var email = formCollection["email"].ToString();

            var booking = new CalendarBooking
            {
                Name = name,
                CalendarId = this.WebContent.RefObjectId.Value,
                Date = date,
                Email = email,
                Deleted = false,
                Password = "",
                UnionId = DataSource.Union.Id,
                CalendarTimeSpanId = timespanid,
                Created = DateTime.Now,
                Edited = DateTime.Now,
                AllDay = false,
            };

            return booking;
        }


        private bool ValidateBooking(CalendarBooking booking)
        {
            if (String.IsNullOrWhiteSpace(booking.Email) || booking.CalendarTimeSpanId <= 0)
                return false;

            return true;
        }

        private string CreateSecurityHash(string valueToHash)
        {
            valueToHash += "-booking";

            return DataProtection.ComputeSHA256Hash(valueToHash);
        }
        private bool ValidateSecurityHash(string hashToValidate, string valueToHash)
        {
            return hashToValidate == CreateSecurityHash(valueToHash);
        }

        public bool Unbook(string hash, int bookingId)
        {
            if (bookingId == -1)
                return false;

            var booking = DataSource.Set<CalendarBooking>().FirstOrDefault(x => x.Id == bookingId);

            var isValidated = ValidateSecurityHash(hash, booking.Id + booking.Name);

            DataSource.TryDelete(booking);

            return true;
        }

        public override IHttpPostResult OnPost(ActionContext actionContext, IHttpPostResult parentHttpPostResult = null)
        {
            var postResult = new HttpPostResult(parentHttpPostResult);
            postResult.Success = true;
            postResult.ActionTaken = true;

            var unBookHashQuery = HttpContext.Request.Query["unbook"].FirstOrDefault();
            var bookingIdQuery = HttpContext.Request.Query["bookingid"].FirstOrDefault();
            // Unbook a booking.
            if (!String.IsNullOrEmpty(unBookHashQuery) && !String.IsNullOrEmpty(bookingIdQuery))
            {
                Unbook(unBookHashQuery, Int32.TryParse(bookingIdQuery, out var bookingId) ? bookingId : -1);
                postResult.ActionResult = new RedirectResult(HttpContext.GetRawUrl().Split('?').First());

            }
            else // Create a booking.
            {
                var emailService = GetService<IEmailService>();
                var formCollection = actionContext.HttpContext.Request.Form;

                var booking = CreateBooking(formCollection);

                var bookingValidated = ValidateBooking(booking);
                bool createdSuccess = false;
                if (bookingValidated)
                    createdSuccess = DataSource.TrySave<CalendarBooking>(booking);

                // Värt att lägga på minnet är localization
                //var hej = Localizer.Get("Tack för din bokning");

                var uniqueValue = booking.Id + booking.Name;
                var hashUrl = CreateSecurityHash(uniqueValue);

                if (createdSuccess)
                {

                    var magiskUrl = $"{HttpContext.GetRawUrl()}?unbook={hashUrl}&bookingid={booking.Id}";
                    var emailBody = String.Format("<h4>Tack för din bokning</h4><p>Klicka på länken för att avboka.</p><a href=\"{0}\">{1}</a>", magiskUrl, magiskUrl);

                    var formMessage = new FormMessageInformation(formCollection["email"], "Bokning");

                    var mm = emailService.CreateMessage(formMessage, emailBody);
                    emailService.Send(mm);
                }

                postResult.ActionResult = new RedirectResult(HttpContext.GetRawUrl());

            }

            return postResult;
        }

        #endregion
    }

    #region Models

    public class CalendarViewModel
    {
        public DateTime WeekDay { get; set; }
        public CalendarDay CalendarDay { get; set; }
        public List<CalendarTimeSpanModel> CalendarTimeSpans { get; set; } = new List<CalendarTimeSpanModel>();
    }

    public class CalendarTimeSpanModel : CalendarTimeSpan
    {
        public bool Unavalible { get; set; } = false;

        public string StartAsTime
        {
            get { return DoubleToTimeString(this.StartHour); }
            set { StartHour = StringTimeToDouble(value); }
        }

        public string EndAsTime
        {
            get { return DoubleToTimeString(this.EndHour); }
            set { EndHour = StringTimeToDouble(value); }
        }

        public string DoubleToTimeString(double value)
        {
            TimeSpan timespan = TimeSpan.FromHours(value);
            string output = timespan.ToString("hh\\:mm", CultureInfo.CurrentCulture);
            return output;
        }

        public double StringTimeToDouble(string value)
        {
            DateTime dt = DateTime.Parse(value);
            float output = (float)dt.TimeOfDay.TotalHours;
            return output;
        }
    }
    #endregion

    /// <summary>
    /// This extracts the first day of the week from the passed DayOfWeek. 
    /// </summary>
    public static class DateTimeExtensions
    {
        public static DateTime StartOfWeek(this DateTime dt, DayOfWeek startOfWeek)
        {
            int diff = (7 + (dt.DayOfWeek - startOfWeek)) % 7;
            return dt.AddDays(-1 * diff).Date;
        }
    }
}



