import Foundation

enum TOEFLQuestionBank {
    private static let formatSource = "Original practice item aligned to the ETS TOEFL iBT 2026 blueprint; not an official ETS question."

    static func fullSections() -> [TOEFLExamSection] {
        let readingRouter = readingRouterQuestions()
        let listeningRouter = listeningRouterQuestions()
        return [
            TOEFLExamSection(
                id: "reading-full",
                kind: .reading,
                durationSeconds: 30 * 60,
                questions: readingRouter + readingModuleQuestions(route: .lower),
                routerCount: readingRouter.count,
                route: nil
            ),
            TOEFLExamSection(
                id: "listening-full",
                kind: .listening,
                durationSeconds: 29 * 60,
                questions: listeningRouter + listeningModuleQuestions(route: .lower),
                routerCount: listeningRouter.count,
                route: nil
            ),
            TOEFLExamSection(
                id: "writing-full",
                kind: .writing,
                durationSeconds: 23 * 60,
                questions: writingQuestions,
                routerCount: nil,
                route: nil
            ),
            TOEFLExamSection(
                id: "speaking-full",
                kind: .speaking,
                durationSeconds: 8 * 60,
                questions: speakingQuestions,
                routerCount: nil,
                route: nil
            )
        ]
    }

    static func practiceSection(for kind: TOEFLSectionKind) -> TOEFLExamSection {
        switch kind {
        case .reading:
            return TOEFLExamSection(id: "reading-practice", kind: .reading, durationSeconds: 10 * 60, questions: Array(readingRouterQuestions().prefix(15)), routerCount: nil, route: nil)
        case .listening:
            return TOEFLExamSection(id: "listening-practice", kind: .listening, durationSeconds: 10 * 60, questions: Array(listeningRouterQuestions().prefix(14)), routerCount: nil, route: nil)
        case .writing:
            return TOEFLExamSection(id: "writing-practice", kind: .writing, durationSeconds: 23 * 60, questions: writingQuestions, routerCount: nil, route: nil)
        case .speaking:
            return TOEFLExamSection(id: "speaking-practice", kind: .speaking, durationSeconds: 8 * 60, questions: speakingQuestions, routerCount: nil, route: nil)
        }
    }

    static func moduleQuestions(for kind: TOEFLSectionKind, route: AdaptiveRoute) -> [TOEFLQuestion] {
        switch kind {
        case .reading: readingModuleQuestions(route: route)
        case .listening: listeningModuleQuestions(route: route)
        case .writing, .speaking: []
        }
    }

    static func validate() -> [String] {
        let sections = fullSections()
        var issues: [String] = []
        let expected: [TOEFLSectionKind: Int] = [.reading: 50, .listening: 47, .writing: 12, .speaking: 11]
        let expectedTasks: [TOEFLSectionKind: [TOEFLTaskType: Int]] = [
            .reading: [.completeWords: 30, .readDailyLife: 10, .readAcademicPassage: 10],
            .listening: [.listenChooseResponse: 17, .listenConversation: 10, .listenAnnouncement: 8, .listenAcademicTalk: 12],
            .writing: [.buildSentence: 10, .writeEmail: 1, .academicDiscussion: 1],
            .speaking: [.listenRepeat: 7, .interview: 4]
        ]
        for section in sections {
            if section.questions.count != expected[section.kind] {
                issues.append("\(section.kind.title) has \(section.questions.count) items; expected \(expected[section.kind] ?? 0).")
            }
            let counts = Dictionary(grouping: section.questions, by: \.taskType).mapValues(\.count)
            if counts != expectedTasks[section.kind] {
                issues.append("\(section.kind.title) task distribution does not match the 2026 blueprint used by this form.")
            }
        }
        let upperForms: [(TOEFLSectionKind, [TOEFLQuestion])] = [
            (.reading, readingRouterQuestions() + readingModuleQuestions(route: .upper)),
            (.listening, listeningRouterQuestions() + listeningModuleQuestions(route: .upper))
        ]
        for (kind, questions) in upperForms {
            let counts = Dictionary(grouping: questions, by: \.taskType).mapValues(\.count)
            if questions.count != expected[kind] || counts != expectedTasks[kind] {
                issues.append("\(kind.title) upper route has an invalid item distribution.")
            }
        }
        let all = sections.flatMap(\.questions)
            + readingModuleQuestions(route: .upper)
            + listeningModuleQuestions(route: .upper)
        let duplicates = Dictionary(grouping: all, by: \.id).filter { $0.value.count > 1 }
        if !duplicates.isEmpty { issues.append("Duplicate question IDs: \(duplicates.keys.sorted().joined(separator: ", "))") }
        for question in all {
            if let correct = question.correctChoiceID, !question.choices.contains(where: { $0.id == correct }) {
                issues.append("\(question.id) has a missing correct choice.")
            }
            if question.taskType == .buildSentence && question.correctSentenceTokens.isEmpty {
                issues.append("\(question.id) has no sentence key.")
            }
        }
        return issues
    }

    // MARK: - Reading

    private static func readingRouterQuestions() -> [TOEFLQuestion] {
        let cloze: [(String, String, String, TOEFLDifficulty)] = [
            ("r-c-01", "The student center offers quiet rooms where learners can con_____ on difficult assignments.", "concentrate", .intermediate),
            ("r-c-02", "Because the archive is fragile, visitors must han___ each document with care.", "handle", .foundational),
            ("r-c-03", "The research team repeated the experiment to con____ that the result was reliable.", "confirm", .intermediate),
            ("r-c-04", "Many plants adjust their growth when the amount of available light dec______.", "decreases", .intermediate),
            ("r-c-05", "The museum's new exhibit exp_____ how maps have shaped human exploration.", "explains", .foundational),
            ("r-c-06", "Students may bor___ a laptop from the library for up to four hours.", "borrow", .foundational),
            ("r-c-07", "The professor asked the class to com____ the two theories before choosing one.", "compare", .foundational),
            ("r-c-08", "Urban trees can red___ summer temperatures by shading streets and buildings.", "reduce", .foundational),
            ("r-c-09", "The committee will ann_____ its decision after reviewing every application.", "announce", .intermediate),
            ("r-c-10", "Scientists use indirect evidence to inf__ conditions that existed in the distant past.", "infer", .intermediate),
            ("r-c-11", "The workshop is designed to str______ students' academic writing skills.", "strengthen", .intermediate),
            ("r-c-12", "A flexible schedule allows volunteers to par________ without missing class.", "participate", .intermediate),
            ("r-c-13", "The survey revealed a gra____ increase in the use of public transportation.", "gradual", .intermediate),
            ("r-c-14", "Researchers must dist_______ between correlation and a direct causal relationship.", "distinguish", .advanced),
            ("r-c-15", "The discovery chal______ a long-standing assumption about early migration.", "challenged", .advanced)
        ]
        return cloze.map { clozeQuestion(id: $0.0, text: $0.1, answer: $0.2, difficulty: $0.3) }
            + readingDailyRouter
            + readingAcademicRouter
    }

    private static func readingModuleQuestions(route: AdaptiveRoute) -> [TOEFLQuestion] {
        let lower: [(String, String, String, TOEFLDifficulty)] = [
            ("r-l-c-01", "The campus bus arr____ every fifteen minutes during the morning.", "arrives", .foundational),
            ("r-l-c-02", "Please ret___ all borrowed equipment before the laboratory closes.", "return", .foundational),
            ("r-l-c-03", "The guide inc_____ a map and a list of emergency phone numbers.", "includes", .foundational),
            ("r-l-c-04", "A short break can help students rem___ focused during a long class.", "remain", .foundational),
            ("r-l-c-05", "The club meets wee___ to plan activities and welcome new members.", "weekly", .foundational),
            ("r-l-c-06", "The weather station rec____ temperature and rainfall every hour.", "records", .foundational),
            ("r-l-c-07", "Most participants agr___ that the revised schedule was easier to follow.", "agreed", .foundational),
            ("r-l-c-08", "The new pathway con_____ the residence hall with the science building.", "connects", .foundational),
            ("r-l-c-09", "Students should rev___ their notes soon after each lecture.", "review", .foundational),
            ("r-l-c-10", "The garden provides hab____ for several kinds of birds and insects.", "habitat", .intermediate),
            ("r-l-c-11", "The instructor div____ the project into three manageable stages.", "divided", .foundational),
            ("r-l-c-12", "Solar panels gen_____ electricity without burning fossil fuels.", "generate", .intermediate),
            ("r-l-c-13", "The article desc_____ how the neighborhood changed over time.", "describes", .foundational),
            ("r-l-c-14", "A clear heading helps readers loc___ information quickly.", "locate", .foundational),
            ("r-l-c-15", "The tutor sug______ several ways to organize the essay.", "suggested", .foundational)
        ]
        let upper: [(String, String, String, TOEFLDifficulty)] = [
            ("r-u-c-01", "The findings cor________ earlier evidence from a separate field study.", "corroborate", .advanced),
            ("r-u-c-02", "A species may pr______ in a changing environment if it can exploit a new resource.", "prosper", .advanced),
            ("r-u-c-03", "The apparent contradiction dis_______ once the measurements were recalibrated.", "disappeared", .advanced),
            ("r-u-c-04", "Historians remain cau_____ about assigning a single cause to the uprising.", "cautious", .advanced),
            ("r-u-c-05", "The policy was intended to mit_____ congestion without limiting access downtown.", "mitigate", .advanced),
            ("r-u-c-06", "Subtle variations in shell shape may ind_____ adaptation to local currents.", "indicate", .intermediate),
            ("r-u-c-07", "The author jux_______ two accounts to reveal their conflicting assumptions.", "juxtaposes", .advanced),
            ("r-u-c-08", "The material remains sta___ across a surprisingly broad range of temperatures.", "stable", .intermediate),
            ("r-u-c-09", "The committee's recommendation was pro_______ rather than mandatory.", "provisional", .advanced),
            ("r-u-c-10", "New evidence comp_____ researchers to revise the original chronology.", "compelled", .advanced),
            ("r-u-c-11", "The ecosystem is res______ enough to recover from occasional flooding.", "resilient", .advanced),
            ("r-u-c-12", "The model delib_______ excludes minor variables to clarify the main relationship.", "deliberately", .advanced),
            ("r-u-c-13", "The inscription is amb_______ and supports more than one interpretation.", "ambiguous", .advanced),
            ("r-u-c-14", "The mechanism fac_______ the exchange of nutrients between the two organisms.", "facilitates", .advanced),
            ("r-u-c-15", "A brief cooling period does not nec________ contradict the long-term trend.", "necessarily", .advanced)
        ]
        let cloze = (route == .upper ? upper : lower).map { clozeQuestion(id: $0.0, text: $0.1, answer: $0.2, difficulty: $0.3) }
        return cloze
            + (route == .upper ? readingDailyUpper : readingDailyLower)
            + (route == .upper ? readingAcademicUpper : readingAcademicLower)
    }

    private static var readingDailyRouter: [TOEFLQuestion] {
        [
            readingChoice("r-d-01", "Library notice", "The west reading room will close at 6 p.m. on Thursday for electrical work. The east reading room and all online services will remain available.", "What should a student who wants to study after 6 p.m. on Thursday do?", ["Use the east reading room", "Wait for online services to return", "Ask staff to reopen the west room", "Visit before electrical work begins"], 0, .foundational),
            readingChoice("r-d-02", "Message from a classmate", "I uploaded the slides, but the file is too large for email. I put it in our shared course folder instead. Let me know if the link does not work.", "Why did the classmate use the shared folder?", ["The slides were not finished", "The email attachment was too large", "The course required a public link", "The recipient requested a printed copy"], 1, .foundational),
            readingChoice("r-d-03", "Campus dining update", "Beginning Monday, reusable containers may be returned at either dining hall. A deposit is charged only when a replacement container is requested before the original is returned.", "When is a deposit charged?", ["Whenever food is purchased", "When a container is returned late", "When a replacement is requested first", "Only at the smaller dining hall"], 2, .intermediate),
            readingChoice("r-d-04", "Workshop registration", "Your place is reserved until noon Friday. To confirm it, open the registration page and select 'Attend.' If you cannot come, please cancel so another student can join.", "What must the reader do to keep the reservation?", ["Reply by email", "Pay a fee", "Choose Attend online", "Arrive before noon"], 2, .foundational),
            readingChoice("r-d-05", "Residence hall announcement", "Water will be unavailable from 9 to 11 tomorrow morning while a valve is replaced. Residents may fill bottles tonight at the first-floor kitchen.", "Why is the first-floor kitchen mentioned?", ["It is where the repair will occur", "It provides water before the shutdown", "It will close after 9", "It has a new valve"], 1, .intermediate)
        ]
    }

    private static var readingDailyLower: [TOEFLQuestion] {
        [
            readingChoice("r-l-d-01", "Gym hours", "The pool opens at 7 a.m. on weekdays and 9 a.m. on weekends. It closes at 8 p.m. every day.", "When does the pool open on Saturday?", ["7 a.m.", "8 a.m.", "9 a.m.", "8 p.m."], 2, .foundational),
            readingChoice("r-l-d-02", "Tutor message", "I can meet Tuesday after class or Wednesday morning. Please tell me which time you prefer by Monday evening.", "What should the reader do by Monday evening?", ["Submit an assignment", "Choose a meeting time", "Attend class", "Cancel Wednesday"], 1, .foundational),
            readingChoice("r-l-d-03", "Bookstore sign", "Textbook returns require a receipt and must be made within fourteen days. Used workbooks with writing cannot be returned.", "Which item can be returned?", ["A marked workbook without a receipt", "A textbook with a receipt after one week", "Any book after one month", "A used workbook with notes"], 1, .foundational),
            readingChoice("r-l-d-04", "Event reminder", "The outdoor film begins at sunset. Bring a blanket; chairs are available only for visitors who requested accessible seating.", "What are most visitors advised to bring?", ["A blanket", "A chair", "A ticket printer", "A lamp"], 0, .foundational),
            readingChoice("r-l-d-05", "Lab email", "The microscope demonstration has moved from Room 204 to Room 118 because the larger screen there is working again.", "Why was the demonstration moved?", ["More microscopes arrived", "Room 204 is larger", "A screen is available in Room 118", "The time changed"], 2, .foundational)
        ]
    }

    private static var readingDailyUpper: [TOEFLQuestion] {
        [
            readingChoice("r-u-d-01", "Advising email", "Although the seminar is listed as full, two seats are held for graduating students. If that describes you, send your degree audit rather than joining the general waitlist.", "What is implied about graduating students?", ["They must avoid the seminar", "They have a separate way to request a seat", "They may skip the degree audit", "They are already enrolled"], 1, .advanced),
            readingChoice("r-u-d-02", "Facilities notice", "The north entrance remains unlocked, but the ramp beside it is being resurfaced. Visitors needing a step-free route should enter through the courtyard until further notice.", "Why might someone use the courtyard entrance?", ["The north entrance is locked", "The courtyard closes later", "The usual accessible route is unavailable", "Parking is free there"], 2, .intermediate),
            readingChoice("r-u-d-03", "Project message", "I incorporated Maya's data, but I left the conclusion unchanged because the new sample reinforces rather than alters our interpretation.", "What effect did Maya's data have?", ["It contradicted the conclusion", "It supported the existing interpretation", "It required a new sample", "It was omitted from the project"], 1, .advanced),
            readingChoice("r-u-d-04", "Grant update", "Applications submitted before the deadline will still be considered under the original criteria. The revised budget limit applies only to proposals received next cycle.", "Who is affected by the revised limit?", ["Current reviewers", "Late applicants in this cycle", "Applicants in the next cycle", "Everyone already considered"], 2, .advanced),
            readingChoice("r-u-d-05", "Course forum", "Several people asked whether the field trip replaces Friday's lecture. It does not; the trip is optional and is intended to extend the case study discussed in class.", "What misconception does the post correct?", ["The trip is required", "The case study was canceled", "The trip replaces a lecture", "Friday's room changed"], 2, .advanced)
        ]
    }

    private static var readingAcademicRouter: [TOEFLQuestion] {
        [
            readingChoice("r-a-01", "Urban heat", "Cities often remain warmer than nearby rural areas after sunset. Pavement and buildings absorb solar energy during the day and release it slowly at night, while vegetation in rural areas cools the air through evaporation.", "What is the main idea of the passage?", ["Rural areas receive less sunlight", "Urban materials and limited vegetation help cities retain heat", "Buildings cool rapidly after sunset", "Evaporation increases pavement temperature"], 1, .intermediate),
            readingChoice("r-a-02", "Seed dispersal", "Some plants produce fruits that attract birds. The birds digest the fruit but pass the seeds intact, often far from the parent plant. This reduces competition between young plants and their parent.", "What benefit does bird dispersal provide?", ["Seeds become larger", "Parent plants produce less fruit", "Young plants grow away from direct competition", "Birds protect plants from insects"], 2, .foundational),
            readingChoice("r-a-03", "Volcanic soils", "Fresh volcanic ash contains few nutrients available to plants. Over time, however, weathering breaks minerals into forms that roots can absorb. Volcanic regions may therefore develop highly fertile soils long after an eruption.", "Why can old volcanic soil be fertile?", ["Ash immediately contains organic matter", "Weathering makes mineral nutrients available", "Eruptions remove plant roots", "Rainfall stops erosion"], 1, .intermediate),
            readingChoice("r-a-04", "Collective memory", "Communities do not remember every past event equally. Public ceremonies, monuments, and school curricula repeatedly emphasize some events, making them central to a shared account of the past.", "According to the passage, what helps make an event central to collective memory?", ["Its exact date", "Repeated public attention", "A lack of written records", "Universal agreement about it"], 1, .intermediate),
            readingChoice("r-a-05", "Animal navigation", "Migrating animals often combine several cues rather than relying on only one. A bird may use the sun when skies are clear and magnetic information when clouds obscure visual landmarks.", "What does the example of the bird show?", ["Magnetic cues work only in sunlight", "Migration depends on a single inherited map", "Different cues can compensate for changing conditions", "Clouds prevent successful migration"], 2, .advanced)
        ]
    }

    private static var readingAcademicLower: [TOEFLQuestion] {
        [
            readingChoice("r-l-a-01", "Coral partnerships", "Tiny algae live inside many corals. The algae use sunlight to make food and share some of it with the coral. In return, the coral provides shelter and materials the algae need.", "How do the algae benefit?", ["They receive shelter and materials", "They avoid sunlight", "They eat the coral", "They move to deeper water"], 0, .foundational),
            readingChoice("r-l-a-02", "Early paper", "Before paper became common, people wrote on materials such as clay, wood, and animal skin. Paper was lighter than many of these materials and could be produced in large sheets.", "What advantage of paper is mentioned?", ["It was waterproof", "It was lighter", "It lasted forever", "It was made of clay"], 1, .foundational),
            readingChoice("r-l-a-03", "Tides", "Ocean tides are strongly influenced by the Moon's gravity. The Sun also affects tides, but because the Moon is much closer to Earth, its tidal influence is especially important.", "Why is the Moon especially important to tides?", ["It is closer to Earth", "It is larger than the Sun", "It produces ocean water", "It moves more slowly"], 0, .foundational),
            readingChoice("r-l-a-04", "Pollination", "When a bee visits a flower for nectar, pollen may stick to its body. Some of that pollen is carried to the next flower, allowing the plant to reproduce.", "What happens when the bee visits the next flower?", ["It makes nectar", "It may deliver pollen", "It loses its wings", "It prevents reproduction"], 1, .foundational),
            readingChoice("r-l-a-05", "Trade routes", "Ancient trade routes carried more than goods. Travelers also exchanged stories, technologies, and religious ideas, influencing societies far from where those ideas began.", "What does the passage emphasize about trade routes?", ["They spread ideas as well as products", "They connected only nearby towns", "They prevented cultural change", "They were used mainly by soldiers"], 0, .intermediate)
        ]
    }

    private static var readingAcademicUpper: [TOEFLQuestion] {
        [
            readingChoice("r-u-a-01", "Dendrochronology", "Tree rings record local growing conditions, but a single tree can produce an unusual ring because of disease or damage. Researchers therefore compare overlapping patterns from many trees before assigning a regional climate signal.", "Why do researchers compare many trees?", ["To identify patterns not caused by an individual tree's condition", "To make every tree produce wider rings", "To determine the species without examining wood", "To eliminate the effect of regional climate"], 0, .advanced),
            readingChoice("r-u-a-02", "Marketplaces", "A marketplace may appear to be merely a site of exchange, yet historians often treat it as a political space. Rules governing who may sell, where stalls are placed, and which measures are accepted reveal how authority operates in daily life.", "Why do historians study marketplace rules?", ["The rules show how authority is exercised", "Markets rarely involve economic exchange", "Stall placement determines climate", "Political leaders always worked as merchants"], 0, .advanced),
            readingChoice("r-u-a-03", "Deep ocean carbon", "When microscopic organisms die near the ocean surface, only a small fraction of their carbon reaches great depth. Most material is consumed or decomposed while sinking. Changes in that fraction, though modest, can alter how long carbon remains outside the atmosphere.", "What can be inferred about carbon that reaches great depth?", ["It returns immediately to the atmosphere", "It may be stored away from the atmosphere for longer", "It is never consumed by organisms", "It forms most surface material"], 1, .advanced),
            readingChoice("r-u-a-04", "Scientific models", "A model's simplifications are not necessarily flaws. By excluding minor influences, a model can reveal a dominant mechanism. Trouble arises only when the excluded influences become important for the question being asked.", "Which statement best expresses the author's view?", ["Models should include every measurable variable", "Simplification can be useful when it fits the research question", "A dominant mechanism cannot be modeled", "Excluded influences are always unimportant"], 1, .advanced),
            readingChoice("r-u-a-05", "Language change", "A new expression may spread rapidly through social networks without remaining in the language. Long-term adoption depends not only on visibility but also on whether speakers find the expression useful across recurring situations.", "What distinction does the passage make?", ["Written and spoken language", "Rapid spread and lasting adoption", "Young and old speakers", "Useful and grammatical expressions"], 1, .advanced)
        ]
    }

    // MARK: - Listening

    private static func listeningRouterQuestions() -> [TOEFLQuestion] {
        listeningResponseSet(prefix: "l-r", difficulty: .intermediate, rows: [
            ("Could you send me the revised schedule?", ["Sure, I'll attach it now.", "The schedule was very crowded.", "I revised my major."], 0),
            ("Why wasn't the lab open this morning?", ["At the science building.", "It was closed for maintenance.", "I opened the file."], 1),
            ("Do you mind if I sit here?", ["Not at all; go ahead.", "I sat there yesterday.", "The chair is made of wood."], 0),
            ("When is your presentation?", ["In the main auditorium.", "About coastal ecosystems.", "Right after lunch on Friday."], 2),
            ("I thought the assignment was due today.", ["The professor extended it until Monday.", "I enjoyed the assignment.", "Today has been busy."], 0),
            ("Could you show me how to reserve a study room?", ["The room was quiet.", "Yes, start by opening the library app.", "I studied for two hours."], 1),
            ("Didn't Maya join the field trip?", ["No, she had another class.", "The field was wet.", "Yes, the trip leaves tomorrow."], 0),
            ("How did the workshop go?", ["It was useful, especially the editing exercise.", "It goes to the second floor.", "At three o'clock."], 0),
            ("Should we print the poster or display it on a screen?", ["The screen is twenty inches.", "Let's display it so we can revise it later.", "I printed my notes yesterday."], 1)
        ]) + [
            listeningChoice("l-r-con-01", .listenConversation, "Student: I registered for the chemistry lab, but it overlaps with my statistics discussion. Adviser: The Tuesday lab has two open seats. If you switch today, the system will preserve your place in the lecture.", "What does the adviser recommend?", ["Drop the chemistry lecture", "Move to the Tuesday lab", "Delay statistics until next year", "Attend both classes at once"], 1, .intermediate),
            listeningChoice("l-r-con-02", .listenConversation, "Student: The book I need is listed as available, but it isn't on the shelf. Librarian: Someone may be using it in the building. I can place a hold, so it cannot be checked out before you get it.", "Why does the librarian offer to place a hold?", ["To order a new edition", "To reserve the book when it is returned", "To allow the student to use another library", "To remove the listing"], 1, .intermediate),
            listeningChoice("l-r-con-03", .listenConversation, "Professor: Your proposal has a clear question, but the project is too broad for six weeks. Student: Would focusing only on neighborhood gardens make it manageable? Professor: Yes, and you could compare just two sites.", "How should the student revise the project?", ["Study fewer sites and a narrower topic", "Collect data for more than six weeks", "Replace the research question", "Work without a proposal"], 0, .intermediate),
            listeningChoice("l-r-con-04", .listenConversation, "Student: I came to pick up my campus card. Staff member: It arrived, but the photo is too dark. We can retake it now; the new card will be ready tomorrow afternoon.", "Why can't the student receive the card now?", ["The office is closing", "The student forgot identification", "The photograph must be replaced", "The card fee was not paid"], 2, .foundational),
            listeningChoice("l-r-con-05", .listenConversation, "Student A: Our survey results are due Monday, but only twelve people responded. Student B: The student union newsletter goes out tonight. Let's ask them to include the link.", "What solution does Student B propose?", ["Change the deadline", "Reduce the survey questions", "Share the survey in a newsletter", "Interview twelve people again"], 2, .intermediate),
            listeningChoice("l-r-ann-01", .listenAnnouncement, "Attention library visitors. The third floor is temporarily closed because a water pipe is being inspected. Materials from that floor can be requested at the main desk and will be retrieved by staff.", "What should visitors do if they need a third-floor book?", ["Return tomorrow", "Ask at the main desk", "Enter through another stairway", "Use the water pipe map"], 1, .foundational),
            listeningChoice("l-r-ann-02", .listenAnnouncement, "Tomorrow's outdoor career fair will be held inside the recreation center because heavy rain is expected. Employer check-in begins at eight thirty, and students may enter at ten as originally scheduled.", "What has changed?", ["The event location", "The student entry time", "The list of employers", "The event date"], 0, .intermediate),
            listeningChoice("l-r-ann-03", .listenAnnouncement, "Students planning to graduate this term should verify the spelling of their names in the online portal by Friday. Corrections received later may not appear in the printed ceremony program.", "Why should students check their names by Friday?", ["To receive ceremony tickets", "To ensure corrections appear in print", "To apply for graduation", "To select a portal password"], 1, .intermediate),
            listeningChoice("l-r-ann-04", .listenAnnouncement, "The campus shuttle will skip the museum stop between noon and two while the street is closed for a parade. Riders should use the temporary stop one block east on Pine Street.", "What are riders advised to do?", ["Avoid travel until evening", "Walk to a temporary stop", "Take the museum shuttle", "Join the parade route"], 1, .foundational),
            listeningChoice("l-r-talk-01", .listenAcademicTalk, "Many desert plants open tiny pores on their leaves at night rather than during the day. They take in carbon dioxide when temperatures are cooler, reducing the amount of water lost through evaporation.", "Why do these plants open their pores at night?", ["To receive more sunlight", "To limit water loss", "To warm their leaves", "To release carbon dioxide"], 1, .intermediate),
            listeningChoice("l-r-talk-02", .listenAcademicTalk, "A city's street pattern can preserve traces of much older land use. A curved road may follow the boundary of a former field, even after buildings replace the field itself.", "What point does the speaker illustrate?", ["Modern roads erase all earlier patterns", "Old land boundaries can influence present streets", "Curved roads are cheaper to build", "Fields are usually located downtown"], 1, .intermediate),
            listeningChoice("l-r-talk-03", .listenAcademicTalk, "When an unfamiliar sound occurs repeatedly without consequences, an animal may gradually stop reacting to it. This process, called habituation, allows attention to be directed toward more useful signals.", "What is one benefit of habituation?", ["It makes every sound louder", "It helps an animal ignore unimportant signals", "It prevents unfamiliar events", "It improves night vision"], 1, .intermediate),
            listeningChoice("l-r-talk-04", .listenAcademicTalk, "Early glass was rarely perfectly clear. Small bubbles and variations in thickness distorted the view through a window, but these imperfections can now help historians identify where and when the glass was produced.", "Why are imperfections useful to historians?", ["They make windows stronger", "They reveal information about production", "They remove bubbles from glass", "They improve the view"], 1, .advanced),
            listeningChoice("l-r-talk-05", .listenAcademicTalk, "Economists distinguish a temporary shortage from a lasting change in supply. A storm may interrupt deliveries for a week, whereas the closure of a major factory can alter supply for much longer.", "How does the speaker explain the distinction?", ["By comparing two causes with different durations", "By describing a single factory", "By rejecting the idea of shortages", "By calculating delivery costs"], 0, .intermediate),
            listeningChoice("l-r-talk-06", .listenAcademicTalk, "Some moons in the outer solar system are warmed by tidal forces. As they orbit, the gravity of a nearby planet repeatedly stretches their interiors, producing friction and heat.", "What produces heat inside these moons?", ["Reflected sunlight", "Repeated gravitational stretching", "Impacts with nearby planets", "Chemical reactions in the atmosphere"], 1, .advanced)
        ]
    }

    private static func listeningModuleQuestions(route: AdaptiveRoute) -> [TOEFLQuestion] {
        if route == .upper { return listeningUpperModule }
        return listeningLowerModule
    }

    private static var listeningLowerModule: [TOEFLQuestion] {
        listeningResponseSet(prefix: "l-l", difficulty: .foundational, rows: [
            ("Where can I charge my laptop?", ["There is an outlet by the window.", "It costs twenty dollars.", "I charged the student fee."], 0),
            ("Are you finished with this newspaper?", ["It is today's edition.", "Yes, you can take it.", "I read every morning."], 1),
            ("Why are you leaving so early?", ["I have a doctor's appointment.", "The exit is over there.", "Early in the morning."], 0),
            ("Which bus goes downtown?", ["The number six stops across the street.", "Downtown is crowded.", "I usually walk to class."], 0),
            ("Would you like some help carrying those boxes?", ["They contain books.", "Yes, thank you.", "I bought them yesterday."], 1),
            ("Did the professor return our quizzes?", ["No, she said they'll be ready Friday.", "The quiz was difficult.", "I returned to class."], 0),
            ("How often does the club meet?", ["In the student center.", "About twenty members.", "Every other Wednesday."], 2),
            ("Is the cafeteria still serving lunch?", ["Yes, until two o'clock.", "Lunch was delicious.", "The cafeteria has tables."], 0)
        ]) + [
            listeningChoice("l-l-con-01", .listenConversation, "Student: My printer stopped halfway through the report. Staff member: You can send the file to the help desk printer. Just write your name on the request form.", "What should the student do?", ["Repair the printer", "Send the file to the help desk", "Rewrite the report", "Buy a request form"], 1, .foundational),
            listeningChoice("l-l-con-02", .listenConversation, "Student: Is the history lecture still in this room? Classmate: No, the sign says it moved upstairs to Room 310.", "Where will the lecture be held?", ["In this room", "Outside", "In Room 310", "In the history office"], 2, .foundational),
            listeningChoice("l-l-con-03", .listenConversation, "Student: I missed the safety training. Can I still work in the lab? Assistant: Not yet. Another training session is offered Thursday, and you can begin afterward.", "What must the student do before working in the lab?", ["Attend Thursday's training", "Ask a classmate", "Change laboratories", "Submit results"], 0, .foundational),
            listeningChoice("l-l-con-04", .listenConversation, "Student A: The study group is meeting at six, right? Student B: We moved it to seven because Elena's class ends late.", "Why was the meeting time changed?", ["The room closes at six", "A member has a late class", "The assignment is due at seven", "Elena canceled"], 1, .foundational),
            listeningChoice("l-l-con-05", .listenConversation, "Student: I paid for a parking permit online. Clerk: Great. Show me the confirmation email, and I'll give you the permit.", "What does the clerk need to see?", ["A driver's test", "A confirmation email", "A class schedule", "A parking map"], 1, .foundational),
            listeningChoice("l-l-ann-01", .listenAnnouncement, "The elevator in the arts building is out of service today. Anyone who cannot use the stairs should call the front desk for assistance.", "Who should contact the front desk?", ["People needing an alternative to the stairs", "Everyone entering the building", "Only arts instructors", "Elevator repair workers"], 0, .foundational),
            listeningChoice("l-l-ann-02", .listenAnnouncement, "The soccer game begins at four, but gates open at three fifteen. Student tickets are free with a campus identification card.", "What do students need for a free ticket?", ["A team uniform", "A campus identification card", "A gate number", "A printed schedule"], 1, .foundational),
            listeningChoice("l-l-ann-03", .listenAnnouncement, "Please remove bicycles from the east rack before Friday morning. The rack will be replaced, and remaining bicycles will be moved to campus storage.", "What will happen Friday?", ["A bicycle race will begin", "The rack will be replaced", "Campus storage will close", "New bicycles will be sold"], 1, .foundational),
            listeningChoice("l-l-ann-04", .listenAnnouncement, "Today's guest lecture starts twenty minutes late because the speaker's train was delayed. The room and topic have not changed.", "What changed about the lecture?", ["The speaker", "The topic", "The start time", "The room"], 2, .foundational),
            listeningChoice("l-l-talk-01", .listenAcademicTalk, "A shadow forms when an object blocks light. The shadow changes size when either the light source or the object moves.", "What can cause a shadow's size to change?", ["Movement of the light or object", "A change in sound", "The object's name", "Only the time of year"], 0, .foundational),
            listeningChoice("l-l-talk-02", .listenAcademicTalk, "Beavers build dams that slow moving water. The resulting ponds create habitat for fish, frogs, insects, and water plants.", "What is one result of a beaver dam?", ["The river becomes an ocean", "A pond habitat forms", "All insects disappear", "Water moves faster"], 1, .foundational),
            listeningChoice("l-l-talk-03", .listenAcademicTalk, "Clay becomes easier to shape when water is added. Heating the finished object in a kiln then makes it hard and durable.", "Why is clay heated in a kiln?", ["To add water", "To change its color only", "To make it hard and durable", "To make it easier to shape"], 2, .foundational),
            listeningChoice("l-l-talk-04", .listenAcademicTalk, "A map's scale shows the relationship between distance on the map and distance in the real world. Without the scale, a reader cannot accurately estimate travel distance.", "What does a map scale allow a reader to estimate?", ["The map's age", "Real-world distance", "The weather", "The number of roads"], 1, .foundational),
            listeningChoice("l-l-talk-05", .listenAcademicTalk, "Some flowers close at night. Closing can protect pollen from cool air and moisture until daytime pollinators return.", "What benefit of closing is mentioned?", ["It attracts nighttime animals", "It protects pollen", "It creates more sunlight", "It changes the plant's roots"], 1, .foundational),
            listeningChoice("l-l-talk-06", .listenAcademicTalk, "Written laws make rules more stable because people can consult the same record. Oral rules, by contrast, may change as they are repeated.", "What advantage of written laws is described?", ["They are shorter", "They provide a stable record", "They require no readers", "They change more quickly"], 1, .intermediate)
        ]
    }

    private static var listeningUpperModule: [TOEFLQuestion] {
        listeningResponseSet(prefix: "l-u", difficulty: .advanced, rows: [
            ("I wouldn't count on the archive staying open late.", ["Then we should finish our research this afternoon.", "The archive contains old newspapers.", "I counted the folders twice."], 0),
            ("Wasn't the revised proposal supposed to be shorter?", ["Yes, but the methods section needed clarification.", "The proposal was submitted online.", "No, the meeting was short."], 0),
            ("The lecture raised more questions than it answered.", ["I agree; the evidence was intriguing but incomplete.", "Please answer the phone.", "The lecture hall has raised seats."], 0),
            ("Could we postpone the interview without losing the participant?", ["Possibly, if we offer another time today.", "The participant studied biology.", "The interview questions are printed."], 0),
            ("I take it the committee rejected our first design.", ["Not entirely; they asked for a less expensive material.", "The design is on the first page.", "I took the committee's notes."], 0),
            ("Why not cite the original study instead of the review?", ["Good point; it would support the claim more directly.", "The review session is optional.", "I studied at the library."], 0),
            ("Would the results hold if the sample were more diverse?", ["That's exactly what the follow-up study should test.", "The sample is in the refrigerator.", "The results were printed in color."], 0),
            ("So the deadline is flexible, within reason?", ["Yes, but we should request an extension before Friday.", "Reason is a philosophical topic.", "The deadline appears in bold."], 0)
        ]) + [
            listeningChoice("l-u-con-01", .listenConversation, "Student: The internship office says I need faculty approval, but my adviser is away. Coordinator: The department chair can sign instead, provided your adviser has already reviewed the placement by email.", "Under what condition can the chair approve the internship?", ["The placement is unpaid", "The adviser has reviewed it by email", "The student changes departments", "The coordinator joins the internship"], 1, .advanced),
            listeningChoice("l-u-con-02", .listenConversation, "Researcher: The sensor readings drift after about an hour. Technician: That model is sensitive to room temperature. Calibrate it after it has been in the lab long enough to reach thermal equilibrium.", "What does the technician imply caused the drift?", ["The experiment was too short", "The sensor had not adjusted to room temperature", "The readings were entered manually", "The model lacked a battery"], 1, .advanced),
            listeningChoice("l-u-con-03", .listenConversation, "Student A: Our introduction promises a historical comparison, but the rest of the paper examines only the current policy. Student B: Then either the introduction or the evidence needs to change. Finding historical data now may be unrealistic.", "What will the students most likely do?", ["Add extensive historical data", "Remove the paper's current evidence", "Revise the introduction to match the evidence", "Abandon the policy topic"], 2, .advanced),
            listeningChoice("l-u-con-04", .listenConversation, "Student: I assumed the fellowship covered travel automatically. Administrator: It can cover travel, but only if the cost was included in your approved budget. Otherwise, you need a separate amendment.", "Why might the student need an amendment?", ["The fellowship has ended", "Travel was not in the approved budget", "The destination is overseas", "The administrator rejected the fellowship"], 1, .advanced),
            listeningChoice("l-u-con-05", .listenConversation, "Professor: Your graph makes the difference look dramatic because the vertical axis begins at ninety. Student: The values really are close together. I'll start the axis at zero and discuss the smaller difference in the caption.", "What concern does the professor raise?", ["The graph may visually exaggerate the difference", "The caption contains the wrong values", "The data should be removed", "The horizontal axis is missing"], 0, .advanced),
            listeningChoice("l-u-ann-01", .listenAnnouncement, "Due to unusually high demand, the ceramics workshop will add a second session rather than enlarge the first. People already registered will keep their original times unless they request a transfer by Wednesday.", "What will happen to current registrants who take no action?", ["They will lose their places", "They will remain in their original sessions", "They will be moved to Wednesday", "They must attend both sessions"], 1, .advanced),
            listeningChoice("l-u-ann-02", .listenAnnouncement, "The anthropology colloquium has been relocated to the smaller seminar room. Attendance is still open, but seats will be available on a first-come basis; the live stream remains available to everyone.", "What is implied about attending in person?", ["It now requires payment", "Space may be limited", "The speaker will appear only online", "Attendance is restricted to anthropology students"], 1, .advanced),
            listeningChoice("l-u-ann-03", .listenAnnouncement, "Maintenance on the data server is expected to finish by midnight. Files will remain readable during the work, but edits made after nine may not be saved. Download anything you need to modify before then.", "Why should users download files before nine?", ["Files will become unreadable", "Later changes may not be preserved", "The server closes permanently", "Downloads require maintenance approval"], 1, .advanced),
            listeningChoice("l-u-ann-04", .listenAnnouncement, "The river sampling trip will proceed despite light rain. It will be canceled only if lightning is observed nearby, in which case participants will receive a text message at least thirty minutes before departure.", "Under what circumstance will the trip be canceled?", ["Any rain", "A late participant", "Nearby lightning", "A delayed text message"], 2, .intermediate),
            listeningChoice("l-u-talk-01", .listenAcademicTalk, "An archaeological layer containing imported pottery might suggest long-distance trade, but the inference is not automatic. The vessels could have arrived generations later as valued heirlooms, so their age and the layer's age must be established independently.", "Why does the speaker mention heirlooms?", ["To show why imported pottery does not always date the trade directly", "To argue that pottery was never traded", "To explain how vessels were manufactured", "To identify the owners of the site"], 0, .advanced),
            listeningChoice("l-u-talk-02", .listenAcademicTalk, "In many markets, a higher price reduces demand. A prestige good can behave differently because its expense contributes to its desirability: purchasing it signals status. Yet even this effect has limits, since an extreme price eventually excludes most buyers.", "What qualification does the speaker make?", ["Prestige goods never signal status", "The unusual demand pattern does not continue without limit", "All buyers prefer inexpensive goods", "Price has no effect on demand"], 1, .advanced),
            listeningChoice("l-u-talk-03", .listenAcademicTalk, "A wetland can reduce flooding by storing storm water, but its effectiveness depends on timing. If the soil is already saturated by earlier rain, the same wetland has far less capacity when another storm arrives.", "What determines whether the wetland can store much additional water?", ["The number of plant species", "How saturated the soil already is", "The distance to a city", "Whether the storm occurs at night"], 1, .advanced),
            listeningChoice("l-u-talk-04", .listenAcademicTalk, "Readers often interpret a narrator's confidence as evidence of reliability. Novelists can exploit that expectation by giving a narrator a polished, certain voice while gradually revealing gaps between the narration and observable events.", "What technique does the speaker describe?", ["Using a confident voice to conceal unreliability", "Removing the narrator from a novel", "Presenting events in chronological order", "Avoiding observable details"], 0, .advanced),
            listeningChoice("l-u-talk-05", .listenAcademicTalk, "In evolutionary terms, a trait's current use need not explain its origin. Feathers may have first improved temperature regulation and only later supported flight. This later recruitment of an existing trait is known as co-option.", "How do feathers illustrate co-option?", ["They disappeared when flight evolved", "A trait that evolved for one role later served another", "Temperature regulation prevented flight", "Flight caused the origin of all feathers"], 1, .advanced),
            listeningChoice("l-u-talk-06", .listenAcademicTalk, "Satellite measurements of sea level cover nearly the entire ocean, whereas tide gauges provide much longer records at particular coasts. Because each source has a different strength, researchers combine them rather than treating either as complete by itself.", "Why are the two sources combined?", ["They measure unrelated phenomena", "Their strengths are complementary", "Tide gauges cover the whole ocean", "Satellites provide the longest local records"], 1, .advanced)
        ]
    }

    // MARK: - Writing and Speaking

    static let writingQuestions: [TOEFLQuestion] = [
        sentenceQuestion("w-s-01", shuffled: ["the campus garden", "because", "students volunteered", "expanded", "last spring"], correct: ["the campus garden", "expanded", "last spring", "because", "students volunteered"]),
        sentenceQuestion("w-s-02", shuffled: ["where", "the seminar", "will be held", "do you know"], correct: ["do you know", "where", "the seminar", "will be held"]),
        sentenceQuestion("w-s-03", shuffled: ["the results", "although", "were unexpected", "they were", "consistent"], correct: ["although", "the results", "were unexpected", "they were", "consistent"]),
        sentenceQuestion("w-s-04", shuffled: ["before", "please submit", "the form", "the office closes"], correct: ["please submit", "the form", "before", "the office closes"]),
        sentenceQuestion("w-s-05", shuffled: ["the lecture", "so clearly", "that", "everyone understood", "explained the process"], correct: ["the lecture", "explained the process", "so clearly", "that", "everyone understood"]),
        sentenceQuestion("w-s-06", shuffled: ["whether", "the data", "we need to determine", "support the hypothesis"], correct: ["we need to determine", "whether", "the data", "support the hypothesis"]),
        sentenceQuestion("w-s-07", shuffled: ["had already left", "by the time", "the train", "we reached the station"], correct: ["by the time", "we reached the station", "the train", "had already left"]),
        sentenceQuestion("w-s-08", shuffled: ["not only", "lowered costs", "but also", "the new system", "saved time"], correct: ["the new system", "not only", "lowered costs", "but also", "saved time"]),
        sentenceQuestion("w-s-09", shuffled: ["students", "are more likely", "when", "to ask questions", "they feel welcome"], correct: ["students", "are more likely", "to ask questions", "when", "they feel welcome"]),
        sentenceQuestion("w-s-10", shuffled: ["the experiment", "if", "fails again", "we will revise", "the procedure"], correct: ["if", "the experiment", "fails again", "we will revise", "the procedure"]),
        TOEFLQuestion(
            id: "w-email-01", section: .writing, taskType: .writeEmail, difficulty: .intermediate,
            prompt: "Your group reserved a media room to record a presentation tomorrow, but the room's microphone is not working. Write an email to the media center coordinator. Explain the problem, describe why the recording is time-sensitive, and request a practical solution.",
            passage: "To: Media Center Coordinator\nSubject: Recording room equipment", audioScript: nil, choices: [], correctChoiceID: nil, acceptedTextAnswers: [], sentenceTokens: [], correctSentenceTokens: [],
            responseGuidance: "Address all three purposes. Use an appropriate greeting, clear request, and courteous closing.",
            sampleAnswer: "Dear Media Center Coordinator,\n\nMy group reserved Media Room 2 for tomorrow, but we found that its microphone is not working. We must submit our recorded presentation on Friday, and tomorrow is the only time all four group members are available. Could you move our reservation to a room with a working microphone or lend us compatible recording equipment? We would be happy to adjust our reservation time if necessary.\n\nThank you for your help.\nBest,\nA student",
            explanation: "Strong responses accomplish the task, elaborate with relevant details, use clear language, and follow email conventions.", sourceNote: formatSource
        ),
        TOEFLQuestion(
            id: "w-discuss-01", section: .writing, taskType: .academicDiscussion, difficulty: .advanced,
            prompt: "Professor: Some universities require every student to complete a community-based project before graduation. Do the educational benefits justify making such a project mandatory? Why or why not?",
            passage: "Lina: A shared project connects classroom ideas with real needs and helps students practice collaboration.\n\nMarcus: A requirement may be unfair to students who work long hours or have caregiving responsibilities.",
            audioScript: nil, choices: [], correctChoiceID: nil, acceptedTextAnswers: [], sentenceTokens: [], correctSentenceTokens: [],
            responseGuidance: "Contribute your own position, support it, and engage with at least one idea already in the discussion.",
            sampleAnswer: "I agree with Marcus that a universal requirement can create unequal burdens, but that problem does not eliminate the educational value Lina identifies. Universities could require the learning outcome while offering flexible formats: a weekend project, remote research for a nonprofit, or recognition of relevant work students already do. With those options, the project would connect academic knowledge to real problems without forcing every student into the same schedule. The requirement is justified only if accessibility is built into its design.",
            explanation: "Strong responses present a relevant, well-supported position with clear organization and varied, accurate language.", sourceNote: formatSource
        )
    ]

    static let speakingQuestions: [TOEFLQuestion] = [
        repeatQuestion("s-r-01", "The orientation session begins near the main entrance.", .foundational),
        repeatQuestion("s-r-02", "Please submit the revised outline before Friday afternoon.", .foundational),
        repeatQuestion("s-r-03", "Our study compares how two neighborhoods adapted to flooding.", .intermediate),
        repeatQuestion("s-r-04", "Students who attend the workshop can borrow recording equipment afterward.", .intermediate),
        repeatQuestion("s-r-05", "Although the evidence is limited, it supports the researchers' initial explanation.", .advanced),
        repeatQuestion("s-r-06", "The committee postponed its recommendation until the environmental review was complete.", .advanced),
        repeatQuestion("s-r-07", "By examining several independent records, historians can distinguish persistent trends from unusual events.", .advanced),
        interviewQuestion("s-i-01", "Let's talk about learning environments. Where do you usually study when you need to concentrate, and what makes that place effective for you?", "Describe the place and explain why it works well.", .intermediate),
        interviewQuestion("s-i-02", "Tell me about a time when another person gave you useful feedback. What did you change as a result?", "Describe the situation, the feedback, and its effect.", .intermediate),
        interviewQuestion("s-i-03", "Some courses let students choose between an individual final project and a group project. Which option would you choose, and why?", "State your choice and develop at least two relevant reasons or examples.", .advanced),
        interviewQuestion("s-i-04", "Universities increasingly record lectures for students to watch later. How might this practice improve learning, and what problem could it create?", "Discuss both a benefit and a possible drawback.", .advanced)
    ]

    // MARK: - Helpers

    private static func clozeQuestion(id: String, text: String, answer: String, difficulty: TOEFLDifficulty) -> TOEFLQuestion {
        TOEFLQuestion(
            id: id, section: .reading, taskType: .completeWords, difficulty: difficulty,
            prompt: "Complete the partially missing word.", passage: text, audioScript: nil,
            choices: [], correctChoiceID: nil, acceptedTextAnswers: [answer], sentenceTokens: [], correctSentenceTokens: [],
            responseGuidance: "Type the complete word, not only the missing letters.", sampleAnswer: answer,
            explanation: "The completed word must fit both the sentence meaning and its grammatical form.", sourceNote: formatSource
        )
    }

    private static func readingChoice(_ id: String, _ title: String, _ passage: String, _ prompt: String, _ answers: [String], _ correct: Int, _ difficulty: TOEFLDifficulty) -> TOEFLQuestion {
        choiceQuestion(id: id, section: .reading, task: id.contains("-d-") ? .readDailyLife : .readAcademicPassage, difficulty: difficulty, prompt: prompt, passage: "\(title)\n\n\(passage)", audio: nil, answers: answers, correct: correct)
    }

    private static func listeningResponseSet(prefix: String, difficulty: TOEFLDifficulty, rows: [(String, [String], Int)]) -> [TOEFLQuestion] {
        rows.enumerated().map { index, row in
            let number = String(format: "%02d", index + 1)
            return listeningChoice("\(prefix)-resp-\(number)", .listenChooseResponse, row.0, "Choose the most appropriate response.", row.1, row.2, difficulty)
        }
    }

    private static func listeningChoice(_ id: String, _ task: TOEFLTaskType, _ audio: String, _ prompt: String, _ answers: [String], _ correct: Int, _ difficulty: TOEFLDifficulty) -> TOEFLQuestion {
        choiceQuestion(id: id, section: .listening, task: task, difficulty: difficulty, prompt: prompt, passage: nil, audio: audio, answers: answers, correct: correct)
    }

    private static func choiceQuestion(id: String, section: TOEFLSectionKind, task: TOEFLTaskType, difficulty: TOEFLDifficulty, prompt: String, passage: String?, audio: String?, answers: [String], correct: Int) -> TOEFLQuestion {
        let ids = answers.indices.map { String(UnicodeScalar(65 + $0)!) }
        return TOEFLQuestion(
            id: id, section: section, taskType: task, difficulty: difficulty, prompt: prompt,
            passage: passage, audioScript: audio,
            choices: zip(ids, answers).map { TOEFLChoice(id: $0.0, text: $0.1) },
            correctChoiceID: ids[correct], acceptedTextAnswers: [], sentenceTokens: [], correctSentenceTokens: [],
            responseGuidance: nil, sampleAnswer: answers[correct],
            explanation: "The keyed response is the choice best supported by the text or recording.", sourceNote: formatSource
        )
    }

    private static func sentenceQuestion(_ id: String, shuffled: [String], correct: [String]) -> TOEFLQuestion {
        TOEFLQuestion(
            id: id, section: .writing, taskType: .buildSentence, difficulty: .intermediate,
            prompt: "Build a grammatical sentence using every phrase.", passage: nil, audioScript: nil,
            choices: [], correctChoiceID: nil, acceptedTextAnswers: [], sentenceTokens: shuffled, correctSentenceTokens: correct,
            responseGuidance: "Tap a selected phrase to return it to the word bank.", sampleAnswer: sentenceCase(correct.joined(separator: " ")) + ".",
            explanation: "The phrases form a complete sentence with standard English word order.", sourceNote: formatSource
        )
    }

    private static func sentenceCase(_ text: String) -> String {
        guard let first = text.first else { return text }
        return first.uppercased() + text.dropFirst()
    }

    private static func repeatQuestion(_ id: String, _ script: String, _ difficulty: TOEFLDifficulty) -> TOEFLQuestion {
        TOEFLQuestion(
            id: id, section: .speaking, taskType: .listenRepeat, difficulty: difficulty,
            prompt: "Repeat the sentence you hear.", passage: nil, audioScript: script,
            choices: [], correctChoiceID: nil, acceptedTextAnswers: [], sentenceTokens: [], correctSentenceTokens: [],
            responseGuidance: "After recording, rate how accurately and intelligibly you reproduced the sentence.", sampleAnswer: script,
            explanation: "Listen and Repeat measures accurate processing and intelligible reproduction of spoken English.", sourceNote: formatSource
        )
    }

    private static func interviewQuestion(_ id: String, _ audio: String, _ guidance: String, _ difficulty: TOEFLDifficulty) -> TOEFLQuestion {
        TOEFLQuestion(
            id: id, section: .speaking, taskType: .interview, difficulty: difficulty,
            prompt: "Respond to the interviewer.", passage: nil, audioScript: audio,
            choices: [], correctChoiceID: nil, acceptedTextAnswers: [], sentenceTokens: [], correctSentenceTokens: [],
            responseGuidance: guidance, sampleAnswer: nil,
            explanation: "Interview responses should be intelligible, fluent, relevant, developed, and use appropriate vocabulary and grammar.", sourceNote: formatSource
        )
    }
}
