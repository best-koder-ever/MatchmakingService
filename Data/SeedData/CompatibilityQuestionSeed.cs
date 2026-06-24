using MatchmakingService.Models;
using Microsoft.EntityFrameworkCore;

namespace MatchmakingService.Data.SeedData
{
    public static class CompatibilityQuestionSeed
    {
        public static async Task SeedAsync(MatchmakingDbContext db)
        {
            var existing = await db.CompatibilityQuestions.CountAsync();
            if (existing >= 32) 
            {
                await EnsureVoiceFieldsAsync(db);
                return;
            }

            // If some questions already exist (15 legacy), only add the new ones
            var existingSortOrders = await db.CompatibilityQuestions
                .Select(q => q.SortOrder)
                .ToListAsync();

            if (existing > 0 && existing < 32)
            {
                await SeedAdditionalQuestionsAsync(db, existingSortOrders);
                return;
            }

            // Fresh seed — all 32 questions

            var questions = new List<CompatibilityQuestion>
            {
                // ── Personality (5 questions) ──
                new() {
                    Category = QuestionCategory.Personality, SortOrder = 1,
                    Emoji = "🎉", TextEn = "Friday night — what sounds best?",
                    TextSv = "Fredagkväll — vad låter bäst?",
                    OptionsJson = """[{"label":"Big party","labelSv":"Stor fest","value":1},{"label":"Dinner with friends","labelSv":"Middag med vänner","value":2},{"label":"Cozy night in","labelSv":"Mysig kväll hemma","value":3},{"label":"Solo adventure","labelSv":"Soloäventyr","value":4}]""",
                    VoiceEligible = true,
                    VoicePromptText = "Describe your perfect Friday night — what are you doing, who are you with?",
                    VoicePromptTextSv = "Beskriv din perfekta fredagkväll — vad gör du, vem är du med?"
                },
                new() {
                    Category = QuestionCategory.Personality, SortOrder = 2,
                    Emoji = "🗣️", TextEn = "In a group, you're usually the one who…",
                    TextSv = "I en grupp brukar du vara den som…",
                    OptionsJson = """[{"label":"Leads the conversation","labelSv":"Leder samtalet","value":1},{"label":"Cracks the jokes","labelSv":"Drar skämten","value":2},{"label":"Listens & asks questions","labelSv":"Lyssnar & ställer frågor","value":3},{"label":"Observes quietly","labelSv":"Observerar tyst","value":4}]"""
                },
                new() {
                    Category = QuestionCategory.Personality, SortOrder = 3,
                    Emoji = "🧠", TextEn = "When making big decisions, you go with…",
                    TextSv = "När du fattar stora beslut litar du på…",
                    OptionsJson = """[{"label":"Gut feeling","labelSv":"Magkänslan","value":1},{"label":"Careful research","labelSv":"Noggrann research","value":2},{"label":"Ask everyone I trust","labelSv":"Frågar alla jag litar på","value":3},{"label":"Flip a coin honestly","labelSv":"Singlar slant ärligt","value":4}]"""
                },
                new() {
                    Category = QuestionCategory.Personality, SortOrder = 4,
                    Emoji = "🌊", TextEn = "Plans change last minute — how do you feel?",
                    TextSv = "Planerna ändras i sista stund — hur känns det?",
                    OptionsJson = """[{"label":"Love it, spontaneity!","labelSv":"Älskar det, spontanitet!","value":1},{"label":"Slightly annoyed but fine","labelSv":"Lite irriterad men okej","value":2},{"label":"Really stressed","labelSv":"Riktigt stressad","value":3}]"""
                },
                new() {
                    Category = QuestionCategory.Personality, SortOrder = 5,
                    Emoji = "💬", TextEn = "After a long day, you recharge by…",
                    TextSv = "Efter en lång dag laddar du om genom att…",
                    OptionsJson = """[{"label":"Calling a friend","labelSv":"Ringa en vän","value":1},{"label":"Going for a walk","labelSv":"Ta en promenad","value":2},{"label":"Total alone time","labelSv":"Total ensamtid","value":3},{"label":"Something creative","labelSv":"Något kreativt","value":4}]"""
                },

                // ── Values (4 questions) ──
                new() {
                    Category = QuestionCategory.Values, SortOrder = 6,
                    Emoji = "❤️", TextEn = "What matters most in a partner?",
                    TextSv = "Vad är viktigast hos en partner?",
                    OptionsJson = """[{"label":"Humor","labelSv":"Humor","value":1},{"label":"Ambition","labelSv":"Ambition","value":2},{"label":"Kindness","labelSv":"Vänlighet","value":3},{"label":"Honesty","labelSv":"Ärlighet","value":4},{"label":"Adventure","labelSv":"Äventyr","value":5}]""",
                    VoiceEligible = true,
                    VoicePromptText = "What do you value most in a partner and why? Tell us what really matters to you.",
                    VoicePromptTextSv = "Vad värderar du mest hos en partner och varför? Berätta vad som verkligen betyder något."
                },
                new() {
                    Category = QuestionCategory.Values, SortOrder = 7,
                    Emoji = "🏠", TextEn = "Your ideal life in 5 years?",
                    TextSv = "Ditt ideala liv om 5 år?",
                    OptionsJson = """[{"label":"City life, career focus","labelSv":"Stadsliv, karriärfokus","value":1},{"label":"Settled down, maybe kids","labelSv":"Bofäst, kanske barn","value":2},{"label":"Traveling the world","labelSv":"Resa runt världen","value":3},{"label":"Countryside, simple life","labelSv":"Landsbygd, enkelt liv","value":4}]""",
                    VoiceEligible = true,
                    VoicePromptText = "Paint a picture of your ideal life in 5 years — where are you, what does a typical day look like?",
                    VoicePromptTextSv = "Måla en bild av ditt ideala liv om 5 år — var är du, hur ser en vanlig dag ut?"
                },
                new() {
                    Category = QuestionCategory.Values, SortOrder = 8,
                    Emoji = "🙏", TextEn = "How important is faith/spirituality?",
                    TextSv = "Hur viktigt är tro/andlighet?",
                    OptionsJson = """[{"label":"Central to my life","labelSv":"Centralt i mitt liv","value":1},{"label":"Somewhat important","labelSv":"Ganska viktigt","value":2},{"label":"Not really","labelSv":"Inte direkt","value":3},{"label":"Not at all","labelSv":"Inte alls","value":4}]"""
                },
                new() {
                    Category = QuestionCategory.Values, SortOrder = 9,
                    Emoji = "👶", TextEn = "Kids?",
                    TextSv = "Barn?",
                    OptionsJson = """[{"label":"Want them someday","labelSv":"Vill ha nån gång","value":1},{"label":"Already have some","labelSv":"Har redan","value":2},{"label":"Don't want kids","labelSv":"Vill inte ha barn","value":3},{"label":"Open to it","labelSv":"Öppen för det","value":4}]"""
                },

                // ── Attachment (3 questions) ──
                new() {
                    Category = QuestionCategory.Attachment, SortOrder = 10,
                    Emoji = "📱", TextEn = "Your partner hasn't texted in a while…",
                    TextSv = "Din partner har inte hört av sig på ett tag…",
                    OptionsJson = """[{"label":"Not worried, they're busy","labelSv":"Inte orolig, de är upptagna","value":1},{"label":"A bit anxious, double-check","labelSv":"Lite orolig, dubbelkollar","value":2},{"label":"I'd rather have space too","labelSv":"Jag vill också ha utrymme","value":3}]"""
                },
                new() {
                    Category = QuestionCategory.Attachment, SortOrder = 11,
                    Emoji = "🤝", TextEn = "In relationships, closeness feels…",
                    TextSv = "I relationer känns närhet…",
                    OptionsJson = """[{"label":"Natural & comforting","labelSv":"Naturligt & tryggt","value":1},{"label":"I want it but it's scary","labelSv":"Jag vill men det är läskigt","value":2},{"label":"I need my independence","labelSv":"Jag behöver mitt oberoende","value":3}]""",
                    VoiceEligible = true,
                    VoicePromptText = "How do you feel about closeness in relationships? What does a healthy balance look like for you?",
                    VoicePromptTextSv = "Hur känner du inför närhet i relationer? Hur ser en sund balans ut för dig?"
                },
                new() {
                    Category = QuestionCategory.Attachment, SortOrder = 12,
                    Emoji = "💔", TextEn = "After an argument, you usually…",
                    TextSv = "Efter ett bråk brukar du…",
                    OptionsJson = """[{"label":"Want to talk it out now","labelSv":"Vill prata ut direkt","value":1},{"label":"Need time, then discuss","labelSv":"Behöver tid, sen diskutera","value":2},{"label":"Avoid the topic","labelSv":"Undviker ämnet","value":3}]"""
                },

                // ── Lifestyle (3 questions) ──
                new() {
                    Category = QuestionCategory.Lifestyle, SortOrder = 13,
                    Emoji = "🌅", TextEn = "Morning person or night owl?",
                    TextSv = "Morgonmänniska eller nattugla?",
                    OptionsJson = """[{"label":"Early bird 🌅","labelSv":"Tidigt uppe 🌅","value":1},{"label":"Night owl 🦉","labelSv":"Nattugla 🦉","value":2},{"label":"Depends on the day","labelSv":"Beror på dagen","value":3}]"""
                },
                new() {
                    Category = QuestionCategory.Lifestyle, SortOrder = 14,
                    Emoji = "🏃", TextEn = "How active are you?",
                    TextSv = "Hur aktiv är du?",
                    OptionsJson = """[{"label":"Gym/sports regularly","labelSv":"Gym/sport regelbundet","value":1},{"label":"Casual walks & yoga","labelSv":"Promenader & yoga","value":2},{"label":"Couch is my gym","labelSv":"Soffan är mitt gym","value":3}]"""
                },
                new() {
                    Category = QuestionCategory.Lifestyle, SortOrder = 15,
                    Emoji = "🍷", TextEn = "Drinking?",
                    TextSv = "Alkohol?",
                    OptionsJson = """[{"label":"Socially","labelSv":"Socialt","value":1},{"label":"Rarely","labelSv":"Sällan","value":2},{"label":"Never","labelSv":"Aldrig","value":3},{"label":"Yes, often","labelSv":"Ja, ofta","value":4}]""",
                    VoiceEligible = true,
                    VoicePromptText = "Tell us about a perfect weekend activity — what do you love doing in your free time?",
                    VoicePromptTextSv = "Berätta om en perfekt helgaktivitet — vad älskar du att göra på fritiden?"
                },

                // ── Personality continued (questions 16-19) ──
                new() {
                    Category = QuestionCategory.Personality, SortOrder = 16,
                    Emoji = "😤", TextEn = "How do you handle stress?",
                    TextSv = "Hur hanterar du stress?",
                    OptionsJson = """[{"label":"Talk it out","labelSv":"Pratar ut om det","value":1},{"label":"Exercise it out","labelSv":"Tränar bort det","value":2},{"label":"Quiet alone time","labelSv":"Tyst ensamtid","value":3},{"label":"Distract myself","labelSv":"Distraherar mig","value":4}]"""
                },
                new() {
                    Category = QuestionCategory.Personality, SortOrder = 17,
                    Emoji = "🎭", TextEn = "You'd describe yourself as…",
                    TextSv = "Du skulle beskriva dig själv som…",
                    OptionsJson = """[{"label":"Serious & focused","labelSv":"Seriös & fokuserad","value":1},{"label":"Playful & goofy","labelSv":"Lekfull & tokig","value":2},{"label":"Calm & grounded","labelSv":"Lugn & jordnära","value":3},{"label":"Intense & passionate","labelSv":"Intensiv & passionerad","value":4}]"""
                },
                new() {
                    Category = QuestionCategory.Personality, SortOrder = 18,
                    Emoji = "🔇", TextEn = "Silence between two people feels…",
                    TextSv = "Tystnad mellan två personer känns…",
                    OptionsJson = """[{"label":"Comfortable, love it","labelSv":"Bekvämt, älskar det","value":1},{"label":"Awkward, I fill it","labelSv":"Besvärande, jag fyller det","value":2},{"label":"Depends on the person","labelSv":"Beror på personen","value":3}]"""
                },
                new() {
                    Category = QuestionCategory.Personality, SortOrder = 19,
                    Emoji = "🌍", TextEn = "Your relationship with travel?",
                    TextSv = "Din relation till resor?",
                    OptionsJson = """[{"label":"Constant explorer","labelSv":"Ständig utforskare","value":1},{"label":"A few trips a year","labelSv":"Några resor per år","value":2},{"label":"Prefer home comforts","labelSv":"Föredrar hemtrevnad","value":3},{"label":"Work trips count","labelSv":"Jobbresorna räknas","value":4}]""",
                    VoiceEligible = true,
                    VoicePromptText = "Tell us about a trip that changed you — where did you go and what did you discover?",
                    VoicePromptTextSv = "Berätta om en resa som förändrade dig — vart åkte du och vad upptäckte du?"
                },

                // ── Values continued (questions 20-24) ──
                new() {
                    Category = QuestionCategory.Values, SortOrder = 20,
                    Emoji = "💼", TextEn = "Career vs. personal life?",
                    TextSv = "Karriär vs. privatliv?",
                    OptionsJson = """[{"label":"Career first right now","labelSv":"Karriär just nu","value":1},{"label":"Balance is everything","labelSv":"Balans är allt","value":2},{"label":"Personal life always wins","labelSv":"Privatliv vinner alltid","value":3},{"label":"Depends on the season","labelSv":"Beror på säsongen","value":4}]"""
                },
                new() {
                    Category = QuestionCategory.Values, SortOrder = 21,
                    Emoji = "💍", TextEn = "Views on marriage?",
                    TextSv = "Syn på äktenskap?",
                    OptionsJson = """[{"label":"Definitely want to marry","labelSv":"Vill absolut gifta mig","value":1},{"label":"Open to it","labelSv":"Öppen för det","value":2},{"label":"Prefer long-term without marriage","labelSv":"Föredrar sambo utan giftemål","value":3},{"label":"Not for me","labelSv":"Inte för mig","value":4}]"""
                },
                new() {
                    Category = QuestionCategory.Values, SortOrder = 22,
                    Emoji = "🐾", TextEn = "Pets?",
                    TextSv = "Husdjur?",
                    OptionsJson = """[{"label":"Have & love them","labelSv":"Har & älskar dem","value":1},{"label":"Want them someday","labelSv":"Vill ha nån gång","value":2},{"label":"Allergic/not my thing","labelSv":"Allergisk/inte min grej","value":3},{"label":"Only if partner wants","labelSv":"Bara om partner vill","value":4}]"""
                },
                new() {
                    Category = QuestionCategory.Values, SortOrder = 23,
                    Emoji = "🗺️", TextEn = "Long distance — could you do it?",
                    TextSv = "Långdistans — kan du tänka dig det?",
                    OptionsJson = """[{"label":"Yes, for the right person","labelSv":"Ja, för rätt person","value":1},{"label":"Short term only","labelSv":"Kort tid bara","value":2},{"label":"No, not for me","labelSv":"Nej, inte för mig","value":3}]"""
                },
                new() {
                    Category = QuestionCategory.Values, SortOrder = 24,
                    Emoji = "💰", TextEn = "Financial style in a relationship?",
                    TextSv = "Ekonomisk stil i en relation?",
                    OptionsJson = """[{"label":"Split everything equally","labelSv":"Delar allt lika","value":1},{"label":"Proportional to income","labelSv":"Proportionellt mot inkomst","value":2},{"label":"One pays, one manages home","labelSv":"En betalar, en sköter hemmet","value":3},{"label":"Completely merged finances","labelSv":"Helt sammanslagna ekonomier","value":4}]"""
                },

                // ── Attachment continued (questions 25-27) ──
                new() {
                    Category = QuestionCategory.Attachment, SortOrder = 25,
                    Emoji = "🆘", TextEn = "When you're going through something hard…",
                    TextSv = "När du går igenom något svårt…",
                    OptionsJson = """[{"label":"I open up immediately","labelSv":"Öppnar mig direkt","value":1},{"label":"I process alone first","labelSv":"Bearbetar ensam först","value":2},{"label":"I downplay it to others","labelSv":"Spelar ned det för andra","value":3},{"label":"I wait until I need help","labelSv":"Väntar tills jag behöver hjälp","value":4}]""",
                    VoiceEligible = true,
                    VoicePromptText = "How do you lean on people when things get tough? What kind of support matters most to you?",
                    VoicePromptTextSv = "Hur lutar du dig mot andra när det blir tufft? Vilken typ av stöd betyder mest för dig?"
                },
                new() {
                    Category = QuestionCategory.Attachment, SortOrder = 26,
                    Emoji = "🔐", TextEn = "Trust in a relationship?",
                    TextSv = "Tillit i en relation?",
                    OptionsJson = """[{"label":"Given freely until broken","labelSv":"Ges fritt tills det bryts","value":1},{"label":"Built slowly over time","labelSv":"Byggs långsamt över tid","value":2},{"label":"I'm cautious by default","labelSv":"Jag är försiktig per default","value":3}]"""
                },
                new() {
                    Category = QuestionCategory.Attachment, SortOrder = 27,
                    Emoji = "🧳", TextEn = "Emotional baggage from past relationships?",
                    TextSv = "Känslomässigt bagage från tidigare relationer?",
                    OptionsJson = """[{"label":"Worked through it all","labelSv":"Bearbetat allt","value":1},{"label":"Some things still linger","labelSv":"Vissa saker hänger kvar","value":2},{"label":"Actively working on it","labelSv":"Jobbar aktivt med det","value":3},{"label":"Just being honest — a lot","labelSv":"Ärligt talat — en del","value":4}]"""
                },

                // ── Lifestyle continued (questions 28-32) ──
                new() {
                    Category = QuestionCategory.Lifestyle, SortOrder = 28,
                    Emoji = "🚬", TextEn = "Smoking?",
                    TextSv = "Rökning?",
                    OptionsJson = """[{"label":"Never","labelSv":"Aldrig","value":1},{"label":"Socially/occasionally","labelSv":"Socialt/ibland","value":2},{"label":"Yes, regularly","labelSv":"Ja, regelbundet","value":3},{"label":"Trying to quit","labelSv":"Försöker sluta","value":4}]"""
                },
                new() {
                    Category = QuestionCategory.Lifestyle, SortOrder = 29,
                    Emoji = "🍽️", TextEn = "Cooking and food?",
                    TextSv = "Matlagning och mat?",
                    OptionsJson = """[{"label":"I love to cook","labelSv":"Älskar att laga mat","value":1},{"label":"I eat out most days","labelSv":"Äter ute oftast","value":2},{"label":"I try new cuisines constantly","labelSv":"Provar ständigt nya kök","value":3},{"label":"Simple & healthy","labelSv":"Enkelt & hälsosamt","value":4}]"""
                },
                new() {
                    Category = QuestionCategory.Lifestyle, SortOrder = 30,
                    Emoji = "🏡", TextEn = "Living situation preference?",
                    TextSv = "Preferens för boendesituation?",
                    OptionsJson = """[{"label":"City centre apartment","labelSv":"Citylägenhet","value":1},{"label":"Suburbs with space","labelSv":"Förort med plats","value":2},{"label":"Rural/countryside","labelSv":"Landsbygd","value":3},{"label":"Doesn't matter","labelSv":"Spelar ingen roll","value":4}]"""
                },
                new() {
                    Category = QuestionCategory.Lifestyle, SortOrder = 31,
                    Emoji = "💻", TextEn = "Screen time in a relationship?",
                    TextSv = "Skärmtid i en relation?",
                    OptionsJson = """[{"label":"Phones away when together","labelSv":"Telefoner bort när vi är ihop","value":1},{"label":"Balance — some screen, some not","labelSv":"Balans — lite skärm, lite inte","value":2},{"label":"Each to their own","labelSv":"Var och en för sig","value":3}]"""
                },
                new() {
                    Category = QuestionCategory.Lifestyle, SortOrder = 32,
                    Emoji = "🌿", TextEn = "How much does sustainability matter to you?",
                    TextSv = "Hur mycket betyder hållbarhet för dig?",
                    OptionsJson = """[{"label":"Very — it shapes my choices","labelSv":"Mycket — det formar mina val","value":1},{"label":"I try where I can","labelSv":"Försöker när jag kan","value":2},{"label":"Not a priority honestly","labelSv":"Inte en prioritet ärligt talat","value":3}]""",
                    VoiceEligible = true,
                    VoicePromptText = "What's one value that's non-negotiable for you in a partner?",
                    VoicePromptTextSv = "Vad är ett värde som är icke-förhandlingsbart för dig hos en partner?"
                },
            };

            db.CompatibilityQuestions.AddRange(questions);
            await db.SaveChangesAsync();
        }

        /// <summary>Append only the missing questions to an existing partial seed (15→32).</summary>
        private static async Task SeedAdditionalQuestionsAsync(MatchmakingDbContext db, List<int> existingSortOrders)
        {
            var allQuestions = GetAll32Questions();
            var newQuestions = allQuestions.Where(q => !existingSortOrders.Contains(q.SortOrder)).ToList();
            if (newQuestions.Count > 0)
            {
                db.CompatibilityQuestions.AddRange(newQuestions);
                await db.SaveChangesAsync();
            }
            await EnsureVoiceFieldsAsync(db);
        }

        private static List<CompatibilityQuestion> GetAll32Questions()
        {
            // Returns all 32 questions — used for diff-seeding
            // (mirrors the fresh-seed list above, kept in sync)
            return new List<CompatibilityQuestion>
            {
                new() { Category = QuestionCategory.Personality, SortOrder = 16, Emoji = "😤", TextEn = "How do you handle stress?", TextSv = "Hur hanterar du stress?", OptionsJson = """[{"label":"Talk it out","labelSv":"Pratar ut om det","value":1},{"label":"Exercise it out","labelSv":"Tränar bort det","value":2},{"label":"Quiet alone time","labelSv":"Tyst ensamtid","value":3},{"label":"Distract myself","labelSv":"Distraherar mig","value":4}]""" },
                new() { Category = QuestionCategory.Personality, SortOrder = 17, Emoji = "🎭", TextEn = "You'd describe yourself as…", TextSv = "Du skulle beskriva dig själv som…", OptionsJson = """[{"label":"Serious & focused","labelSv":"Seriös & fokuserad","value":1},{"label":"Playful & goofy","labelSv":"Lekfull & tokig","value":2},{"label":"Calm & grounded","labelSv":"Lugn & jordnära","value":3},{"label":"Intense & passionate","labelSv":"Intensiv & passionerad","value":4}]""" },
                new() { Category = QuestionCategory.Personality, SortOrder = 18, Emoji = "🔇", TextEn = "Silence between two people feels…", TextSv = "Tystnad mellan två personer känns…", OptionsJson = """[{"label":"Comfortable, love it","labelSv":"Bekvämt, älskar det","value":1},{"label":"Awkward, I fill it","labelSv":"Besvärande, jag fyller det","value":2},{"label":"Depends on the person","labelSv":"Beror på personen","value":3}]""" },
                new() { Category = QuestionCategory.Personality, SortOrder = 19, Emoji = "🌍", TextEn = "Your relationship with travel?", TextSv = "Din relation till resor?", OptionsJson = """[{"label":"Constant explorer","labelSv":"Ständig utforskare","value":1},{"label":"A few trips a year","labelSv":"Några resor per år","value":2},{"label":"Prefer home comforts","labelSv":"Föredrar hemtrevnad","value":3},{"label":"Work trips count","labelSv":"Jobbresorna räknas","value":4}]""", VoiceEligible = true, VoicePromptText = "Tell us about a trip that changed you — where did you go and what did you discover?", VoicePromptTextSv = "Berätta om en resa som förändrade dig — vart åkte du och vad upptäckte du?" },
                new() { Category = QuestionCategory.Values, SortOrder = 20, Emoji = "💼", TextEn = "Career vs. personal life?", TextSv = "Karriär vs. privatliv?", OptionsJson = """[{"label":"Career first right now","labelSv":"Karriär just nu","value":1},{"label":"Balance is everything","labelSv":"Balans är allt","value":2},{"label":"Personal life always wins","labelSv":"Privatliv vinner alltid","value":3},{"label":"Depends on the season","labelSv":"Beror på säsongen","value":4}]""" },
                new() { Category = QuestionCategory.Values, SortOrder = 21, Emoji = "💍", TextEn = "Views on marriage?", TextSv = "Syn på äktenskap?", OptionsJson = """[{"label":"Definitely want to marry","labelSv":"Vill absolut gifta mig","value":1},{"label":"Open to it","labelSv":"Öppen för det","value":2},{"label":"Prefer long-term without marriage","labelSv":"Föredrar sambo utan giftemål","value":3},{"label":"Not for me","labelSv":"Inte för mig","value":4}]""" },
                new() { Category = QuestionCategory.Values, SortOrder = 22, Emoji = "🐾", TextEn = "Pets?", TextSv = "Husdjur?", OptionsJson = """[{"label":"Have & love them","labelSv":"Har & älskar dem","value":1},{"label":"Want them someday","labelSv":"Vill ha nån gång","value":2},{"label":"Allergic/not my thing","labelSv":"Allergisk/inte min grej","value":3},{"label":"Only if partner wants","labelSv":"Bara om partner vill","value":4}]""" },
                new() { Category = QuestionCategory.Values, SortOrder = 23, Emoji = "🗺️", TextEn = "Long distance — could you do it?", TextSv = "Långdistans — kan du tänka dig det?", OptionsJson = """[{"label":"Yes, for the right person","labelSv":"Ja, för rätt person","value":1},{"label":"Short term only","labelSv":"Kort tid bara","value":2},{"label":"No, not for me","labelSv":"Nej, inte för mig","value":3}]""" },
                new() { Category = QuestionCategory.Values, SortOrder = 24, Emoji = "💰", TextEn = "Financial style in a relationship?", TextSv = "Ekonomisk stil i en relation?", OptionsJson = """[{"label":"Split everything equally","labelSv":"Delar allt lika","value":1},{"label":"Proportional to income","labelSv":"Proportionellt mot inkomst","value":2},{"label":"One pays, one manages home","labelSv":"En betalar, en sköter hemmet","value":3},{"label":"Completely merged finances","labelSv":"Helt sammanslagna ekonomier","value":4}]""" },
                new() { Category = QuestionCategory.Attachment, SortOrder = 25, Emoji = "🆘", TextEn = "When you're going through something hard…", TextSv = "När du går igenom något svårt…", OptionsJson = """[{"label":"I open up immediately","labelSv":"Öppnar mig direkt","value":1},{"label":"I process alone first","labelSv":"Bearbetar ensam först","value":2},{"label":"I downplay it to others","labelSv":"Spelar ned det för andra","value":3},{"label":"I wait until I need help","labelSv":"Väntar tills jag behöver hjälp","value":4}]""", VoiceEligible = true, VoicePromptText = "How do you lean on people when things get tough? What kind of support matters most to you?", VoicePromptTextSv = "Hur lutar du dig mot andra när det blir tufft? Vilken typ av stöd betyder mest för dig?" },
                new() { Category = QuestionCategory.Attachment, SortOrder = 26, Emoji = "🔐", TextEn = "Trust in a relationship?", TextSv = "Tillit i en relation?", OptionsJson = """[{"label":"Given freely until broken","labelSv":"Ges fritt tills det bryts","value":1},{"label":"Built slowly over time","labelSv":"Byggs långsamt över tid","value":2},{"label":"I'm cautious by default","labelSv":"Jag är försiktig per default","value":3}]""" },
                new() { Category = QuestionCategory.Attachment, SortOrder = 27, Emoji = "🧳", TextEn = "Emotional baggage from past relationships?", TextSv = "Känslomässigt bagage från tidigare relationer?", OptionsJson = """[{"label":"Worked through it all","labelSv":"Bearbetat allt","value":1},{"label":"Some things still linger","labelSv":"Vissa saker hänger kvar","value":2},{"label":"Actively working on it","labelSv":"Jobbar aktivt med det","value":3},{"label":"Just being honest — a lot","labelSv":"Ärligt talat — en del","value":4}]""" },
                new() { Category = QuestionCategory.Lifestyle, SortOrder = 28, Emoji = "🚬", TextEn = "Smoking?", TextSv = "Rökning?", OptionsJson = """[{"label":"Never","labelSv":"Aldrig","value":1},{"label":"Socially/occasionally","labelSv":"Socialt/ibland","value":2},{"label":"Yes, regularly","labelSv":"Ja, regelbundet","value":3},{"label":"Trying to quit","labelSv":"Försöker sluta","value":4}]""" },
                new() { Category = QuestionCategory.Lifestyle, SortOrder = 29, Emoji = "🍽️", TextEn = "Cooking and food?", TextSv = "Matlagning och mat?", OptionsJson = """[{"label":"I love to cook","labelSv":"Älskar att laga mat","value":1},{"label":"I eat out most days","labelSv":"Äter ute oftast","value":2},{"label":"I try new cuisines constantly","labelSv":"Provar ständigt nya kök","value":3},{"label":"Simple & healthy","labelSv":"Enkelt & hälsosamt","value":4}]""" },
                new() { Category = QuestionCategory.Lifestyle, SortOrder = 30, Emoji = "🏡", TextEn = "Living situation preference?", TextSv = "Preferens för boendesituation?", OptionsJson = """[{"label":"City centre apartment","labelSv":"Citylägenhet","value":1},{"label":"Suburbs with space","labelSv":"Förort med plats","value":2},{"label":"Rural/countryside","labelSv":"Landsbygd","value":3},{"label":"Doesn't matter","labelSv":"Spelar ingen roll","value":4}]""" },
                new() { Category = QuestionCategory.Lifestyle, SortOrder = 31, Emoji = "💻", TextEn = "Screen time in a relationship?", TextSv = "Skärmtid i en relation?", OptionsJson = """[{"label":"Phones away when together","labelSv":"Telefoner bort när vi är ihop","value":1},{"label":"Balance — some screen, some not","labelSv":"Balans — lite skärm, lite inte","value":2},{"label":"Each to their own","labelSv":"Var och en för sig","value":3}]""" },
                new() { Category = QuestionCategory.Lifestyle, SortOrder = 32, Emoji = "🌿", TextEn = "How much does sustainability matter to you?", TextSv = "Hur mycket betyder hållbarhet för dig?", OptionsJson = """[{"label":"Very — it shapes my choices","labelSv":"Mycket — det formar mina val","value":1},{"label":"I try where I can","labelSv":"Försöker när jag kan","value":2},{"label":"Not a priority honestly","labelSv":"Inte en prioritet ärligt talat","value":3}]""", VoiceEligible = true, VoicePromptText = "What's one value that's non-negotiable for you in a partner?", VoicePromptTextSv = "Vad är ett värde som är icke-förhandlingsbart för dig hos en partner?" },
            };
        }

        /// <summary>Backfill voice fields on existing questions (idempotent).</summary>
        private static async Task EnsureVoiceFieldsAsync(MatchmakingDbContext db)
        {
            var voiceMap = new Dictionary<int, (string en, string sv)>
            {
                [1] = ("Describe your perfect Friday night — what are you doing, who are you with?",
                       "Beskriv din perfekta fredagkväll — vad gör du, vem är du med?"),
                [6] = ("What do you value most in a partner and why? Tell us what really matters to you.",
                       "Vad värderar du mest hos en partner och varför? Berätta vad som verkligen betyder något."),
                [7] = ("Paint a picture of your ideal life in 5 years — where are you, what does a typical day look like?",
                       "Måla en bild av ditt ideala liv om 5 år — var är du, hur ser en vanlig dag ut?"),
                [11] = ("How do you feel about closeness in relationships? What does a healthy balance look like for you?",
                        "Hur känner du inför närhet i relationer? Hur ser en sund balans ut för dig?"),
                [15] = ("Tell us about a perfect weekend activity — what do you love doing in your free time?",
                        "Berätta om en perfekt helgaktivitet — vad älskar du att göra på fritiden?")
            };

            var questions = await db.CompatibilityQuestions
                .Where(q => voiceMap.Keys.Contains(q.Id))
                .ToListAsync();

            var updated = false;
            foreach (var q in questions)
            {
                if (!q.VoiceEligible && voiceMap.TryGetValue(q.Id, out var prompts))
                {
                    q.VoiceEligible = true;
                    q.VoicePromptText = prompts.en;
                    q.VoicePromptTextSv = prompts.sv;
                    updated = true;
                }
            }

            if (updated)
                await db.SaveChangesAsync();
        }
    }
}
