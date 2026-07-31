#!/usr/bin/env python3
"""Build the offline 2026 TOEFL practice bank from authorized local study data.

The reference books in ``../References`` include mostly pre-2026 test forms.  They
are used here only to define topic coverage, campus contexts, and tested reading /
listening skills.  This script writes new practice items; it does not copy retired
test questions or claim that any item is an official ETS question.

Complete the Words stimuli are also generated from the new campus and academic
content below, keeping the exam bank original and the authorized books outside the
application bundle.
"""

from __future__ import annotations

import json
import random
import re
from collections import Counter
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "TOEFL Simulation" / "Resources" / "ExpandedQuestionBank.json"

ORIGINAL_SOURCE = (
    "Original expanded practice item informed by the user-authorized reference "
    "library and aligned to the ETS TOEFL iBT 2026 blueprint; not an official ETS question."
)


def blank_question(
    *,
    item_id: str,
    section: str,
    task: str,
    difficulty: str,
    prompt: str,
    passage: str | None = None,
    audio: str | None = None,
    choices: list[dict[str, str]] | None = None,
    correct_choice: str | None = None,
    accepted: list[str] | None = None,
    sentence_tokens: list[str] | None = None,
    correct_tokens: list[str] | None = None,
    guidance: str | None = None,
    sample: str | None = None,
    explanation: str = "",
    source: str = ORIGINAL_SOURCE,
) -> dict:
    return {
        "id": item_id,
        "section": section,
        "taskType": task,
        "difficulty": difficulty,
        "prompt": prompt,
        "passage": passage,
        "audioScript": audio,
        "choices": choices or [],
        "correctChoiceID": correct_choice,
        "acceptedTextAnswers": accepted or [],
        "sentenceTokens": sentence_tokens or [],
        "correctSentenceTokens": correct_tokens or [],
        "responseGuidance": guidance,
        "sampleAnswer": sample,
        "explanation": explanation,
        "sourceNote": source,
    }


def choice_item(
    *,
    item_id: str,
    section: str,
    task: str,
    difficulty: str,
    prompt: str,
    answers: list[str],
    correct: int,
    passage: str | None = None,
    audio: str | None = None,
    explanation: str,
) -> dict:
    ids = ["A", "B", "C", "D"]
    ordered = list(enumerate(answers))
    option_seed = sum((index + 1) * byte for index, byte in enumerate(item_id.encode("utf-8")))
    random.Random(option_seed).shuffle(ordered)
    correct_position = next(index for index, pair in enumerate(ordered) if pair[0] == correct)
    return blank_question(
        item_id=item_id,
        section=section,
        task=task,
        difficulty=difficulty,
        prompt=prompt,
        passage=passage,
        audio=audio,
        choices=[{"id": key, "text": pair[1]} for key, pair in zip(ids, ordered)],
        correct_choice=ids[correct_position],
        sample=answers[correct],
        explanation=explanation,
    )


def difficulty(index: int) -> str:
    return ("foundational", "intermediate", "advanced")[index % 3]


def first_upper(text: str) -> str:
    """Uppercase only the first character, preserving names such as Room 210."""
    return text[:1].upper() + text[1:]


def period(text: str) -> str:
    """End a sentence once, including after abbreviations such as p.m."""
    return text if text.endswith(".") else text + "."


# Campus situations reflect the registrar, library, laboratory, advising, housing,
# events, transportation, and student-service contexts repeatedly represented in
# the authorized reference inventory.  All wording and details below are new.
CAMPUS_CONTEXTS = [
    ("library citation workshop", "scheduled for Tuesday at 3:00", "meet Wednesday at 4:00 in Room 210", "the original room is being repaired", "update your calendar", "Tuesday at noon", "Students registered for the live stream do not need to act", "online participants"),
    ("chemistry safety orientation", "planned for Lab 4 on Monday", "take place in Science Hall 118 on Monday", "Lab 4's ventilation system is being inspected", "download the revised room map", "Sunday evening", "Students who completed the orientation last term remain exempt", "previously certified students"),
    ("campus garden volunteer shift", "set for Saturday morning", "begin Sunday at 9:00 a.m.", "heavy rain is forecast for Saturday", "confirm that you can attend", "Friday at 5:00 p.m.", "The indoor seed-sorting team will still meet Saturday", "the indoor team"),
    ("economics review session", "listed in Lecture Hall A", "meet in Business Center 305", "another course needs the larger hall for an exam", "use the east entrance", "Thursday morning", "The session time remains unchanged", "students concerned only about the time"),
    ("museum sketching tour", "scheduled to begin at the west door", "start at the sculpture courtyard", "construction blocks the west walkway", "meet beside the blue information sign", "Friday at noon", "Visitors with mobility questions may call for an alternate route", "visitors needing an alternate route"),
    ("media-room reservation", "booked for Studio 2 at 2:00", "move to Studio 5 at 2:30", "Studio 2's microphone has failed", "accept the replacement booking online", "tomorrow at 10:00 a.m.", "Groups that bring their own recorder may keep the original time", "groups with their own recorder"),
    ("student health webinar", "announced as an in-person seminar", "be delivered online at the same time", "the presenter cannot travel to campus", "use the link in the confirmation email", "one hour before the event", "Existing registrations will transfer automatically", "already registered students"),
    ("north residence-hall inspection", "planned for all floors on Friday", "cover floors one through three Friday and the remaining floors Monday", "the inspection team has fewer staff members than expected", "check the schedule posted by the elevator", "Thursday night", "Residents do not need to remain in their rooms", "residents who expected to wait inside"),
    ("riverside field trip", "expected to leave at 8:00 a.m.", "depart at 7:30 a.m. from the south gate", "road work will slow the route after nine", "arrive fifteen minutes early", "the night before departure", "The return time is not expected to change", "participants concerned only about the return"),
    ("career-center interview clinic", "open to walk-in visitors", "require a twenty-minute appointment", "last week's walk-in line exceeded room capacity", "choose an available time in the portal", "Wednesday at 6:00 p.m.", "Students seeking only résumé pickup may still walk in", "résumé-pickup visitors"),
    ("astronomy viewing night", "planned for the roof observatory", "use the planetarium instead", "cloud cover will prevent outdoor observations", "enter through the north lobby", "tonight at 7:00", "The event will still begin at the advertised time", "visitors concerned only about the start time"),
    ("graduate funding briefing", "advertised for new doctoral students", "also admit final-year master's students", "several relevant travel grants opened early", "bring a student identification card", "Monday afternoon", "Undergraduates remain ineligible for this session", "undergraduate students"),
    ("archive reading-room appointment", "approved for Thursday morning", "begin Thursday at 1:00 p.m.", "the requested collection needs additional conservation review", "reply to confirm the later time", "Wednesday at noon", "Requests for the digital collection are unaffected", "digital-only researchers"),
    ("campus shuttle stop", "located outside the recreation center", "move temporarily to Oak Street", "utility crews need access to the usual lane", "follow the orange pedestrian signs", "before Monday morning", "The shuttle timetable will not change", "riders concerned only about departure times"),
    ("language-partner meeting", "assigned to pairs by the program office", "let participants choose from three small groups", "an odd number of students registered", "select a group in the course site", "Friday at noon", "Anyone who already arranged a pair may keep it", "students with an existing partner"),
    ("photography exhibit opening", "scheduled for the main gallery", "open first in the east gallery", "installation in the main gallery is running late", "show the digital invitation at the east desk", "Saturday at 4:00 p.m.", "The artist's talk will remain in the auditorium", "guests attending only the artist's talk"),
    ("statistics tutoring session", "offered only on Wednesday", "add a second session Thursday evening", "the Wednesday appointments filled quickly", "reserve only one of the two sessions", "Tuesday at noon", "Existing Wednesday appointments remain valid", "students already booked for Wednesday"),
    ("international-student document check", "available in the central office", "operate from a desk in the student union", "the central office is receiving new furniture", "bring printed copies rather than originals", "the morning of the appointment", "Urgent visa cases may still call the central office", "students with urgent visa cases"),
    ("robotics club demonstration", "designed as a forty-minute event", "run for sixty minutes", "the team added a hands-on trial", "tell the organizer about accessibility needs", "Thursday evening", "Audience members may leave before the trial begins", "visitors unable to stay for the trial"),
    ("environmental science lecture", "listed as required for the introductory class", "be optional for students who completed the field module", "the field module now covers the same material", "check your completion status online", "Friday morning", "All other students must still attend", "students with a completed field module"),
    ("campus food-pantry pickup", "arranged alphabetically by last name", "use numbered appointment windows", "the alphabetic lines caused long waits", "find your number in the confirmation message", "before arriving", "Emergency walk-in service remains available after 4:00", "emergency walk-in visitors"),
    ("theater costume workshop", "limited to drama majors", "open remaining seats to all students", "major enrollment did not fill the room", "submit the short safety form", "Monday at 9:00 a.m.", "Drama majors keep their reserved seats", "drama majors with reservations"),
    ("psychology research signup", "posted on a paper bulletin board", "move entirely to the participant website", "researchers need faster eligibility screening", "complete the online profile", "before choosing a study", "People already scheduled will keep their appointments", "participants with appointments"),
    ("music-practice-room checkout", "available until 10:00 p.m.", "end at 9:00 p.m. this week", "the evening supervisor is attending training", "return keys before the temporary closing time", "each evening this week", "Rooms in the residence hall follow their normal schedule", "residence-hall musicians"),
    ("student newspaper meeting", "planned as an editorial meeting", "include a short design tutorial", "several new editors requested layout training", "bring a laptop with the design program installed", "Tuesday evening", "Writers who cannot attend may submit articles as usual", "writers submitting remotely"),
    ("biology specimen pickup", "handled at the laboratory counter", "use refrigerated locker 12", "the counter will be closed during inventory", "enter the code sent by the technician", "Friday at 3:00 p.m.", "Live specimens still require staff assistance", "students collecting live specimens"),
    ("first-year advising appointment", "set as a group meeting", "be divided into individual ten-minute meetings", "students submitted very different course plans", "upload your proposed schedule", "the day before advising", "General orientation questions will be answered in a shared video", "students with only general questions"),
    ("poetry reading", "announced as free seating", "use reserved seat numbers", "fire-safety staff reduced the room capacity", "collect a seat number at the ticket desk", "thirty minutes before the reading", "The lobby audio feed requires no seat number", "listeners using the lobby feed"),
    ("financial-aid document deadline", "shown as May 10 in an old brochure", "be May 17 for the current cycle", "the online form opened one week late", "submit through the secure portal", "May 17 at 5:00 p.m.", "Scholarship recommendation deadlines have not changed", "students submitting only recommendations"),
    ("engineering makerspace training", "planned around the laser cutter", "focus on the 3-D printers", "a replacement laser lens has not arrived", "watch the printer-safety video first", "Wednesday morning", "Previously approved laser-cutter users keep their certification", "already certified laser users"),
    ("campus film screening", "scheduled outdoors on the lawn", "move into the student center ballroom", "strong wind could damage the screen", "enter through the ballroom's west doors", "Friday at 6:30 p.m.", "The discussion afterward remains in Room 104", "people attending only the discussion"),
    ("history honors presentation", "listed as a single afternoon session", "be split between morning and afternoon sessions", "more students qualified than expected", "check the revised speaking order", "Monday at noon", "Faculty judges will attend both sessions", "faculty judges"),
    ("aquatic-center lap-swim period", "available from 6:00 to 8:00 a.m.", "begin at 6:30 a.m. for two weeks", "a youth team received temporary pool time", "review the lane-allocation chart", "before your next visit", "Weekend hours remain unchanged", "weekend swimmers"),
    ("community-service fair", "expected to use the entire central plaza", "occupy the covered half of the plaza", "a heat advisory limits prolonged sun exposure", "visit the online directory before arriving", "Saturday morning", "Organizations offering indoor projects will also have booths inside", "indoor-project organizations"),
    ("bookstore textbook return", "accepted only at the service counter", "also be accepted through the parcel window", "the counter line has been unusually long", "include the printed return receipt", "the final return date", "Opened access-code packages still require counter review", "students returning opened access codes"),
]


ACADEMIC_TOPICS = [
    ("Urban heat storage", "environmental science", "Cities often cool more slowly than nearby countryside after sunset.", "Dark pavement and dense building materials absorb solar energy and release it gradually.", "Temperature sensors show the largest night-time difference in districts with little tree cover.", "Adding shade and vegetation can reduce local heat exposure.", "Wind and humidity can change the size of the effect from one night to another.", "A shaded courtyard may be cooler than an unshaded parking lot only a block away.", "built surfaces and limited vegetation help cities retain heat"),
    ("Seed dispersal by birds", "ecology", "Fruit-eating birds often carry viable seeds away from a parent plant.", "The fruit is digested while many hard-coated seeds pass through the bird intact.", "Seedlings are frequently found beneath perches far from the source tree.", "Protecting bird habitat can also support forest regeneration.", "Not every plant benefits because some seeds are damaged during digestion.", "A bird that rests along a fence can deposit seeds in a new field.", "birds can reduce plant competition by moving seeds to new sites"),
    ("Volcanic soil development", "geology", "Fresh volcanic deposits may eventually support unusually productive soils.", "Weathering releases mineral nutrients, while colonizing organisms add organic matter.", "Older deposits commonly support more plant growth than newly cooled ash nearby.", "The fertility of volcanic land develops over time rather than immediately after an eruption.", "Heavy rainfall can remove nutrients before roots capture them.", "Lichens growing on bare rock begin the slow process of soil formation.", "weathering and biological activity gradually make volcanic deposits fertile"),
    ("Tree-ring climate records", "earth science", "Patterns in annual tree rings can preserve evidence of past growing conditions.", "Water and temperature affect growth, which changes ring width and wood density.", "Researchers match overlapping patterns from many living and dead trees.", "Cross-checking many samples can extend local climate records far beyond instruments.", "Disease or physical damage can alter one tree's ring without reflecting regional climate.", "Several narrow rings across many trees may mark a prolonged drought.", "shared tree-ring patterns can reconstruct past local climate"),
    ("Wetland flood storage", "hydrology", "Wetlands can temporarily hold water that would otherwise move downstream quickly.", "Shallow basins and absorbent soils slow runoff during a storm.", "Gauges often record lower peak flow below intact wetlands than below drained sites.", "Wetland protection can be one part of a broader flood-management plan.", "A wetland already saturated by earlier rain has little remaining storage capacity.", "A marsh may spread storm water across a wide area before releasing it slowly.", "wetlands reduce some flood peaks by delaying storm runoff"),
    ("Coral and algae partnership", "marine biology", "Many reef-building corals depend on microscopic algae living inside their tissues.", "The algae provide energy-rich compounds, while the coral supplies shelter and nutrients.", "Corals that lose their algae during heat stress become pale and grow poorly.", "Stable water conditions help preserve the partnership that builds reefs.", "Some corals can survive brief bleaching by feeding more actively.", "Sunlit shallow water lets the resident algae photosynthesize efficiently.", "coral growth depends strongly on a nutrient exchange with resident algae"),
    ("Coastal upwelling", "oceanography", "Some coasts support rich fisheries even when surface waters initially contain few nutrients.", "Winds move surface water offshore and allow cold, nutrient-rich deep water to rise.", "Plankton blooms often appear soon after sustained favorable winds.", "Changes in wind patterns can quickly affect coastal food webs.", "Extremely strong upwelling may bring low-oxygen water close to shore.", "A satellite image can reveal a cool band of recently upwelled water.", "wind-driven upwelling supplies nutrients to productive coastal ecosystems"),
    ("Magnetic bird navigation", "animal behavior", "Migrating birds can maintain direction when clouds hide familiar landmarks.", "Specialized sensory processes allow them to use information from Earth's magnetic field.", "Orientation experiments change when researchers alter the magnetic field around a test enclosure.", "Navigation is more reliable when animals can combine magnetic and visual cues.", "Magnetic information may indicate direction but not identify a precise destination.", "A cloudy sky can make the magnetic cue more important than the Sun.", "birds combine magnetic information with other cues during migration"),
    ("Island species balance", "biogeography", "The number of species on an island reflects both arrival and local extinction.", "Nearby large islands receive immigrants more often and support larger populations.", "Surveys typically find more species on large islands close to a mainland source.", "Habitat size and isolation should both be considered in reserve design.", "Human transport can introduce species in ways the simple model does not predict.", "A small distant island may lose a rare species before another population arrives.", "island size and isolation jointly shape species diversity"),
    ("Soil microbial networks", "biology", "Plants obtain some nutrients through interactions with fungi and bacteria around their roots.", "Microbes transform compounds into usable forms and sometimes extend the root's effective reach.", "Plants grown in sterile soil often respond differently from those with intact microbial communities.", "Agricultural practices that protect soil communities may improve nutrient cycling.", "Some microbes are harmful, so more microbial activity is not always beneficial.", "Fungal threads can transport phosphorus from tiny soil pores to a root.", "root-associated microbes can expand plants' access to nutrients"),
    ("Glacier reflectivity", "climate science", "A glacier's surface color influences how much solar energy it absorbs.", "Clean snow reflects much of the incoming light, whereas dust and exposed ice are darker.", "Measurements show faster melting after soot or sediment accumulates on the surface.", "Reducing dark airborne particles may slow melt in some regions.", "Air temperature and snowfall still exert major control over a glacier's total mass.", "A thin layer of soot can lower the reflectivity of otherwise bright snow.", "darker glacier surfaces absorb more energy and melt faster"),
    ("Pollinator timing", "ecology", "Flowering plants and their pollinators must be active at overlapping times.", "Temperature affects the seasonal development of plants and insects at different rates.", "Long-term records reveal years when flowers open before their usual pollinators emerge.", "Climate effects should be evaluated across interacting species, not one species alone.", "Generalist pollinators may compensate when a specialized partner is absent.", "An early spring can shift bloom time more than a bee's emergence date.", "unequal seasonal shifts can separate flowers from their pollinators"),
    ("Pottery source analysis", "archaeology", "The appearance of a ceramic vessel does not always reveal where it was made.", "Clay contains mineral signatures that can be compared with deposits from known locations.", "Laboratory results sometimes link visually similar vessels to different production centers.", "Material analysis can clarify ancient exchange networks.", "Craft workers occasionally transported clay, which complicates a direct location match.", "Two bowls with the same painted design may contain different mineral mixtures.", "mineral evidence can identify pottery origins more reliably than style alone"),
    ("Coin hoards", "history", "Groups of buried coins provide a selective record of economic life.", "People usually hid money during periods of danger and intended to retrieve it later.", "The newest coin in a hoard supplies an earliest possible date for its burial.", "Hoards can reveal moments of insecurity as well as patterns of circulation.", "A hoard overrepresents coins considered worth saving and is not a random sample.", "Coins buried near a frontier may coincide with a documented invasion.", "buried coin groups reveal both chronology and human responses to danger"),
    ("Manuscript copying", "literary history", "Hand-copied texts often exist in versions with small differences.", "Scribes introduced accidental omissions and sometimes made deliberate clarifications.", "Shared unusual errors help scholars identify which copies are related.", "Comparing variants can reconstruct parts of a text's transmission history.", "The earliest surviving copy is not necessarily closest to the lost original.", "Two manuscripts that omit the same line may descend from a common copy.", "copying variations allow scholars to trace relationships among manuscripts"),
    ("Trade routes and ideas", "world history", "Long-distance trade moved knowledge as well as physical goods.", "Merchants, travelers, and translators carried techniques and beliefs between communities.", "Similar artistic motifs appear along routes that linked otherwise distant regions.", "Exchange networks can explain cultural change without requiring conquest.", "Communities often modified imported ideas rather than adopting them unchanged.", "A weaving technique may travel with merchants and acquire a local pattern.", "trade networks spread and transform cultural practices"),
    ("Printing and standardization", "history of technology", "Movable-type printing encouraged wider circulation of consistent texts.", "Printers could reproduce many copies from one arranged set of type.", "Later editions show more stable spelling and page references than many manuscripts.", "Mass reproduction supported shared reference systems for readers.", "Printers still introduced errors, and regional conventions remained.", "Scholars in different cities could cite the same numbered page.", "printing increased textual consistency without eliminating variation"),
    ("Theater acoustics", "architecture", "The shape and materials of a performance space affect speech intelligibility.", "Reflected sound can reinforce a voice, but delayed echoes blur rapid words.", "Acoustic tests improve when absorbent panels reduce long reverberation.", "Successful halls balance sound strength with clarity.", "An empty hall behaves differently after an audience absorbs sound.", "A curved wall may direct an actor's voice toward distant seats.", "performance spaces must manage reflections to keep speech clear"),
    ("Pigment aging", "art conservation", "A painting's present colors may differ from those intended by the artist.", "Light and chemical reactions gradually alter some pigments and varnishes.", "Protected paint beneath a frame is sometimes brighter than exposed paint.", "Conservators use material evidence when proposing a historical color reconstruction.", "Cleaning can also change appearance, so treatment decisions require caution.", "A once-purple mineral pigment may fade toward gray in strong light.", "chemical change can obscure the original appearance of artworks"),
    ("Linear perspective", "art history", "Artists can create an impression of depth on a flat surface through geometric perspective.", "Parallel lines are represented as converging toward shared vanishing points.", "Measured drawings show objects shrinking predictably with apparent distance.", "Perspective offers a systematic tool rather than a complete model of human vision.", "Artists may deliberately distort the system for emphasis or composition.", "Railway tracks in a picture seem to meet near the horizon.", "geometric rules help artists represent spatial depth"),
    ("Memory retrieval cues", "psychology", "Remembering depends partly on cues available when information is retrieved.", "A cue activates associations formed during the original experience.", "Participants often recall more when the test context resembles the learning context.", "Studying with varied cues can make later access more flexible.", "A familiar cue cannot recover information that was never encoded well.", "A particular smell may bring back details of an earlier place.", "retrieval improves when cues reactivate stored associations"),
    ("Spaced practice", "learning science", "Study sessions separated over time often produce longer-lasting learning than one long session.", "Some forgetting between sessions requires the learner to reconstruct the memory.", "Delayed tests commonly favor students who used several shorter sessions.", "Schedules should include repeated retrieval rather than only rereading.", "Massed practice can still help when immediate performance is the only goal.", "Three brief reviews across a week may outlast a single evening of study.", "effortful retrieval across spaced sessions strengthens long-term learning"),
    ("Confirmation bias", "cognitive science", "People often evaluate new evidence in ways that favor an existing belief.", "Attention and interpretation are guided by expectations already in memory.", "Experiments find that participants scrutinize opposing evidence more critically.", "Explicitly considering alternative explanations can improve reasoning.", "Prior knowledge can also guide efficient judgment and is not always misleading.", "Two readers may judge the same ambiguous report differently.", "existing beliefs can shape how people interpret ambiguous evidence"),
    ("Language change", "linguistics", "Small differences in speech can spread through a community over generations.", "Speakers accommodate to social groups, and frequent variants become conventional.", "Recordings from different decades reveal systematic shifts in pronunciation.", "Variation within one generation can be evidence of change in progress.", "Migration and media contact can accelerate or redirect a local trend.", "A pronunciation first associated with young speakers may later become ordinary.", "social transmission turns individual speech variation into language change"),
    ("Price elasticity", "economics", "Demand does not respond equally to price changes for every product.", "Buyers react more strongly when substitutes are available or a purchase can be delayed.", "Sales of optional goods often fall more sharply than sales of necessities after equal price increases.", "Organizations need product-specific evidence when predicting a price change's effect.", "Consumer income and the time period measured can alter the response.", "Travelers may postpone a vacation but still purchase essential medicine.", "available alternatives make demand more responsive to price"),
    ("Network effects", "economics", "Some services become more useful as more people adopt them.", "Each additional user increases the number of possible connections or exchanges.", "Communication platforms often grow rapidly after reaching a critical user base.", "Early adoption patterns can have lasting market effects.", "Crowding and spam can eventually reduce value for users.", "A messaging system is of little use if none of a person's contacts use it.", "growing participation can increase a network service's value"),
    ("Public goods", "political economy", "Some benefits are difficult to limit only to those who pay for them.", "Once provided, these goods can serve many users without being consumed by one person.", "Voluntary funding often falls short when individuals expect others to contribute.", "Collective financing can address underprovision of broadly shared benefits.", "Not every government service is a pure public good because access may be restricted.", "A lighthouse signal can guide ships whether or not each captain paid for it.", "shared, nonexclusive benefits create a funding problem"),
    ("Groundwater recharge", "hydrology", "Underground water supplies are renewed when surface water moves through soil and rock.", "Permeable materials allow infiltration, while pavement redirects water into drains.", "Monitoring wells rise after sustained rain most clearly in undeveloped recharge zones.", "Land-use planning can protect areas where aquifers receive water.", "Deep aquifers may recharge so slowly that recent rainfall has little immediate effect.", "A grassy basin lets storm water soak downward instead of leaving through a pipe.", "permeable recharge zones help replenish groundwater"),
    ("Plate movement", "geology", "Earth's surface plates move slowly relative to one another.", "Heat-driven mantle motion and gravitational forces act on the rigid plates.", "Matching rock belts and magnetic patterns support reconstructions of earlier plate positions.", "Plate motion explains linked patterns of earthquakes, volcanoes, and mountain building.", "Local stress can produce earthquakes away from a plate boundary.", "Symmetric magnetic stripes record seafloor spreading on both sides of a ridge.", "multiple lines of evidence reveal the movement of tectonic plates"),
    ("Exoplanet transits", "astronomy", "A planet can sometimes be detected when it crosses in front of a distant star.", "The planet blocks a small fraction of starlight at regular intervals.", "Repeated dips of the same depth and timing support an orbital interpretation.", "Transit timing and depth can estimate a planet's orbit and relative size.", "The method detects only systems whose orbital plane is aligned with the observer.", "A large planet produces a deeper brightness dip than a small one around the same star.", "regular stellar dimming can reveal an orbiting planet"),
]


BUILD_CONTEXTS = [
    ("the researchers", "repeated the measurements", "documented the uncertainty", "compare several samples", "publish a clearer estimate"),
    ("the students", "revised the outline", "strengthened the conclusion", "review the evidence", "submit a coherent report"),
    ("the volunteers", "mapped the trail", "reported the damaged bridge", "inspect each route", "recommend a safer path"),
    ("the historians", "translated the letters", "identified a missing date", "compare several archives", "clarify the event sequence"),
    ("the biologists", "tagged the birds", "recorded their migration", "monitor seasonal movement", "test the navigation model"),
    ("the engineers", "adjusted the sensor", "reduced the measurement drift", "calibrate the device", "collect more reliable data"),
    ("the librarians", "extended the loan period", "notified all borrowers", "track popular materials", "revise the lending policy"),
    ("the editors", "shortened the introduction", "preserved the central argument", "check every citation", "release the final article"),
    ("the gardeners", "added native plants", "attracted more pollinators", "limit pesticide use", "support local insects"),
    ("the planners", "surveyed bus riders", "changed the evening route", "analyze travel patterns", "reduce passenger delays"),
    ("the curators", "examined the pigment", "revised the display label", "consult conservation records", "explain the color change"),
    ("the economists", "updated the forecast", "acknowledged the weak demand", "compare several indicators", "offer a cautious projection"),
    ("the astronomers", "combined two data sets", "detected a regular signal", "observe repeated transits", "estimate the planet's orbit"),
    ("the geologists", "sampled the ash layer", "confirmed the eruption date", "match mineral signatures", "reconstruct the landscape"),
    ("the designers", "tested the prototype", "simplified the controls", "observe new users", "improve the interface"),
    ("the instructors", "recorded the demonstration", "added written captions", "review learner feedback", "make the lesson accessible"),
    ("the musicians", "changed the arrangement", "balanced the quieter instruments", "rehearse in the hall", "refine the performance"),
    ("the chemists", "cooled the solution", "isolated the target compound", "control the temperature", "repeat the reaction safely"),
    ("the ecologists", "restored the wetland", "measured lower flood peaks", "monitor storm runoff", "evaluate the restoration"),
    ("the psychologists", "expanded the sample", "replicated the memory effect", "randomize participant groups", "test the result again"),
    ("the translators", "consulted the author", "resolved the ambiguous phrase", "compare the surrounding paragraphs", "preserve the intended tone"),
    ("the architects", "modeled the auditorium", "reduced the delayed echo", "measure reflected sound", "improve speech clarity"),
    ("the technicians", "installed the update", "restored the missing files", "back up each device", "prevent another data loss"),
    ("the sociologists", "coded the interviews", "identified a shared concern", "compare participant accounts", "describe the broader pattern"),
    ("the physicians", "reviewed the trial data", "reported the minor side effects", "monitor every participant", "assess the treatment carefully"),
    ("the archaeologists", "analyzed the clay", "traced the vessel's origin", "compare mineral profiles", "map the exchange network"),
    ("the linguists", "examined old recordings", "found a pronunciation shift", "track younger speakers", "document the language change"),
    ("the farmers", "planted cover crops", "reduced soil erosion", "protect the bare fields", "retain more nutrients"),
    ("the programmers", "rewrote the search tool", "shortened the response time", "profile repeated queries", "optimize the database"),
    ("the analysts", "checked the raw figures", "corrected the misleading graph", "use a complete scale", "present the difference accurately"),
]


DISCUSSION_TOPICS = [
    ("mandatory community projects", "Should every student complete a community-based project before graduation?", "projects connect coursework with real needs", "fixed requirements can burden working students", "offer several accessible project formats"),
    ("recorded lectures", "Should universities record most lectures for later viewing?", "recordings let students review difficult explanations", "easy access may reduce live attendance", "combine recordings with participation activities"),
    ("group final projects", "Should a major final project normally be completed in groups?", "collaboration mirrors many professional settings", "unequal contributions can distort assessment", "include individual reflections and role records"),
    ("first-year seminars", "Should all new students take a small first-year seminar?", "small classes can build academic confidence", "one required course may not fit every program", "provide discipline-specific seminar choices"),
    ("public transportation subsidies", "Should universities subsidize local public transportation for all students?", "lower fares can reduce traffic and pollution", "students who walk may receive little direct benefit", "fund the program while improving walking facilities"),
    ("open educational resources", "Should instructors replace commercial textbooks with free materials whenever possible?", "free access reduces financial barriers", "some courses need specialized edited resources", "prefer open materials when quality is comparable"),
    ("attendance policies", "Should attendance count toward a university course grade?", "regular presence supports discussion and practice", "health and work obligations make rigid rules unfair", "grade meaningful participation with reasonable exceptions"),
    ("internship credit", "Should unpaid internships qualify for academic credit?", "supervised work can provide valuable applied learning", "credit may legitimize positions that exclude low-income students", "require learning plans and offer financial support"),
    ("campus green space", "Should a university preserve green space even when it needs more buildings?", "green areas support well-being and biodiversity", "limited land may be needed for classrooms and housing", "use compact construction and protect key habitats"),
    ("art requirements", "Should students in every major take at least one arts course?", "arts study can develop observation and interpretation", "additional requirements reduce room for specialization", "allow arts courses to satisfy broad elective goals"),
    ("AI writing tools", "Should students be allowed to use generative tools during early writing drafts?", "tools can support brainstorming and revision", "students may avoid developing independent judgment", "permit disclosed use with process evidence"),
    ("late-work flexibility", "Should instructors accept late assignments without a penalty?", "flexibility recognizes unexpected personal difficulties", "unlimited extensions can undermine planning and feedback", "offer a small grace period and documented exceptions"),
    ("campus employment", "Should universities guarantee an on-campus job to students who request one?", "jobs provide income and convenient experience", "guarantees may create positions with little educational value", "prioritize need and provide quality standards"),
    ("study abroad", "Should every undergraduate be encouraged to study abroad?", "living elsewhere can broaden cultural understanding", "cost and family duties make travel inaccessible", "support travel while offering local intercultural options"),
    ("pass-fail electives", "Should students be able to take more elective courses pass-fail?", "lower risk may encourage intellectual exploration", "less precise grades may weaken motivation", "limit the option to courses outside the major"),
    ("weekend library hours", "Should a university library stay open all night on weekends?", "late access supports students with varied schedules", "overnight staffing and energy use are expensive", "extend hours during peak exam periods"),
    ("student course evaluations", "Should student evaluations strongly influence instructor promotion?", "students directly observe teaching practices", "ratings can reflect popularity and bias", "combine evaluations with peer review and learning evidence"),
    ("required statistics", "Should students in all majors complete a basic statistics course?", "data literacy helps people judge public claims", "one technical requirement may displace more relevant study", "offer statistics through discipline-based examples"),
    ("single-use plastics", "Should campuses prohibit most single-use plastic food containers?", "a ban can reduce persistent waste", "alternatives may cost more and fail some accessibility needs", "phase in reusable systems with exemptions"),
    ("quiet residence floors", "Should residence halls designate entire floors as quiet areas?", "a shared expectation can improve sleep and study", "strict zones may limit social interaction", "let residents choose quiet housing voluntarily"),
    ("online office hours", "Should every instructor offer at least one online office-hour period?", "remote access helps commuters and working students", "some problems are easier to solve face to face", "combine online and in-person availability"),
    ("gap years", "Should universities make it easy for admitted students to defer enrollment for a year?", "a planned year can build maturity and purpose", "students may lose academic momentum", "require a clear plan while preserving admission"),
    ("peer tutoring", "Should advanced students receive course credit for peer tutoring?", "teaching others deepens knowledge and supports classmates", "credit requires reliable supervision and assessment", "use training, observation, and reflective work"),
    ("physical education", "Should universities require a physical-activity course?", "structured activity can support lifelong health", "a uniform requirement may overlook disability and preference", "allow broad adapted and wellness options"),
    ("research participation", "Should introductory psychology students be required to join research studies?", "participation reveals how evidence is produced", "students may feel pressured to share personal information", "offer equivalent non-participation assignments"),
    ("digital-only notices", "Should universities deliver official notices only through digital channels?", "digital delivery is fast and reduces paper use", "important messages can be lost among many alerts", "use a central archive with urgent reminders"),
    ("parking fees", "Should campus parking fees be increased to fund sustainable transportation?", "higher fees can reduce driving and support transit", "commuters without alternatives may be unfairly burdened", "pair fees with need-based permits and better transit"),
    ("faculty-designed curricula", "Should students have a formal vote when departments revise curricula?", "students can identify practical gaps in courses", "they may not know long-term disciplinary needs", "give students an advisory role alongside faculty"),
    ("entrepreneurship courses", "Should every university offer entrepreneurship courses outside business programs?", "project planning can benefit many careers", "not every student needs a commercial focus", "offer optional courses built around varied social goals"),
    ("local admissions preference", "Should public universities give some admissions preference to local applicants?", "local access can serve the institution's public mission", "preference may exclude stronger distant candidates", "use moderate preference with transparent standards"),
]


INTERVIEW_TOPICS = [
    ("learning environments", "studying in a place where you could concentrate", "quiet surroundings", "easy access to classmates", "your usual study space becomes unavailable", "more universities create reservable individual study rooms"),
    ("feedback", "receiving advice that improved your work", "direct criticism", "questions that guide reflection", "you disagree with a teacher's feedback", "courses use anonymous peer review"),
    ("group work", "contributing successfully to a team", "choosing teammates", "having assigned teammates", "one member misses an important deadline", "more projects are completed by remote teams"),
    ("time management", "finishing a difficult task on schedule", "a detailed calendar", "a short priority list", "two major deadlines occur on the same day", "apps automatically plan students' study time"),
    ("campus events", "attending an event you remember well", "a large public festival", "a small discussion", "an outdoor event must move inside", "more campus events include online audiences"),
    ("reading habits", "reading something that changed your thinking", "printed books", "digital texts", "a required reading seems irrelevant", "students increasingly use short summaries before full texts"),
    ("transportation", "choosing an efficient way to travel", "public transportation", "a bicycle", "your usual route closes unexpectedly", "cities limit private cars near universities"),
    ("volunteering", "helping with a community activity", "one long-term project", "several short projects", "few volunteers arrive for an event", "universities recognize service on transcripts"),
    ("technology", "learning to use a useful new tool", "experimenting independently", "following a formal lesson", "the tool fails during a presentation", "classes use more automated feedback systems"),
    ("exercise", "finding a physical activity you enjoy", "individual exercise", "team sports", "bad weather prevents an outdoor activity", "campuses replace some parking with recreation space"),
    ("food choices", "trying a meal from another culture", "many inexpensive options", "a smaller menu with local ingredients", "the dining hall removes your usual choice", "universities publish the environmental impact of meals"),
    ("leadership", "taking responsibility for a shared goal", "a leader who decides quickly", "a leader who seeks consensus", "team members want incompatible plans", "student organizations rotate leadership roles"),
    ("creative work", "making or designing something you valued", "working with clear limits", "having complete freedom", "your first idea cannot be produced", "digital tools make creative work easier to share"),
    ("language learning", "using another language outside class", "daily conversation practice", "focused grammar study", "you cannot remember a word while speaking", "translation devices become common in classrooms"),
    ("career exploration", "discovering an occupation that interested you", "a short internship", "an informational interview", "your preferred internship is unavailable", "universities add career projects to regular courses"),
    ("environmental action", "changing a habit to reduce waste", "individual choices", "institutional rules", "people ignore a new recycling system", "campuses track energy use in real time"),
    ("museum visits", "seeing an exhibit that taught you something", "interactive displays", "traditional labeled objects", "a key exhibit closes before your visit", "museums offer detailed virtual tours"),
    ("problem solving", "solving a problem through careful planning", "acting quickly", "collecting more information", "the available information conflicts", "teams use computer models before making decisions"),
    ("music and performance", "watching or joining a performance", "live performance", "a high-quality recording", "a performer becomes unavailable", "concerts use more visual technology"),
    ("housing", "making a living space comfortable", "living near campus", "having more private space", "your residence becomes noisy at night", "universities build more shared apartment-style housing"),
    ("news", "following a story that mattered to you", "one trusted news source", "several different sources", "two reliable reports disagree", "more people receive news through short videos"),
    ("mentoring", "learning something from a more experienced person", "regular scheduled meetings", "informal help when needed", "your mentor gives very little direction", "online mentoring connects students across countries"),
    ("travel", "visiting a place that surprised you", "planning every detail", "leaving time for spontaneous choices", "transportation is canceled during a trip", "popular destinations limit daily visitor numbers"),
    ("science communication", "hearing a complex idea explained clearly", "using visual examples", "using a simple analogy", "an audience misunderstands a key term", "researchers share more work directly with the public"),
    ("traditions", "participating in a family or community tradition", "preserving traditions unchanged", "adapting them for new circumstances", "you introduce the tradition to someone unfamiliar with it", "communities celebrate traditional events online"),
]


def build_complete_words() -> list[dict]:
    corpus: list[str] = []
    for event, old, new, reason, action, deadline, exception, _ in CAMPUS_CONTEXTS:
        corpus.extend([
            f"The {event} was {old}.",
            f"The {event} will now {new}.",
            f"The plan changed because {reason}.",
            period(f"Please {action} by {deadline}"),
            period(exception),
        ])
    for _, _, observation, mechanism, evidence, implication, limitation, example, _ in ACADEMIC_TOPICS:
        corpus.extend([observation, mechanism, evidence, implication, limitation, example])
    for _, _, pro, con, compromise in DISCUSSION_TOPICS:
        corpus.extend([
            f"One argument is that {pro}.",
            f"One concern is that {con}.",
            f"A balanced policy could {compromise}.",
        ])

    candidates: list[tuple[str, str]] = []
    for index, sentence in enumerate(dict.fromkeys(corpus)):
        words = [match.group(0) for match in re.finditer(r"\b[A-Za-z]{5,16}\b", sentence)]
        if not words:
            continue
        candidates.append((sentence, words[index % len(words)]))
    if len(candidates) < 360:
        raise RuntimeError(f"Only {len(candidates)} original sentences can be used")

    result = []
    for index, (sentence, answer) in enumerate(candidates[:360], start=1):
        reveal = max(2, min(5, round(len(answer) * 0.38)))
        partial = answer[:reveal] + "_" * (len(answer) - reveal)
        passage = re.sub(rf"\b{re.escape(answer)}\b", partial, sentence, count=1)
        result.append(blank_question(
            item_id=f"x-r-word-{index:03d}", section="reading", task="completeWords",
            difficulty=difficulty(index), prompt="Complete the partially missing word.",
            passage=passage, accepted=list(dict.fromkeys([answer, answer.lower()])), sample=answer,
            explanation=f"“{answer}” restores both the sentence meaning and the required grammatical form.",
        ))
    return result


def build_daily_reading() -> list[dict]:
    result = []
    for context_index, context in enumerate(CAMPUS_CONTEXTS[:30]):
        event, old, new, reason, action, deadline, exception, exception_answer = context
        for variant in range(2):
            if variant == 0:
                body = (f"The {event} was originally {old}, but it will now {new}. This adjustment is necessary because {reason}. "
                        f"{period(f'Please {action} by {deadline}')} {period(exception)}")
            else:
                body = (f"Update for the {event}: because {reason}, the activity will now {new} rather than remain {old}. "
                        f"{period(f'Please {action} by {deadline}')} {period(f'Note that {exception.lower()}')}")
            title = f"Campus update · {event.title()}"
            passage = f"{title}\n\n{body}"
            prefix = f"x-r-daily-{context_index + 1:02d}-{variant + 1}"
            diff = difficulty(context_index + variant)
            result.extend([
                choice_item(item_id=f"{prefix}-p", section="reading", task="readDailyLife", difficulty=diff,
                            prompt="What is the main purpose of this message?", passage=passage,
                            answers=[f"To announce revised arrangements for the {event}", "To advertise a new academic program", "To request payment for a campus service", "To report the results of a research project"], correct=0,
                            explanation="The message centers on a change to the activity and the action participants should take."),
                choice_item(item_id=f"{prefix}-r", section="reading", task="readDailyLife", difficulty=diff,
                            prompt=f"Why were the arrangements for the {event} changed?", passage=passage,
                            answers=[first_upper(reason), "Too few people expressed interest", "The event has received permanent funding", "Participants requested a longer vacation"], correct=0,
                            explanation="The stated reason directly explains the revised arrangement."),
                choice_item(item_id=f"{prefix}-e", section="reading", task="readDailyLife", difficulty=diff,
                            prompt="Who is specifically described as unaffected or exempt?", passage=passage,
                            answers=[first_upper(exception_answer), "All first-year students", "Every campus visitor", "Only the event organizers"], correct=0,
                            explanation="The final sentence identifies the group for whom the usual action or change does not apply."),
            ])
    return result


def academic_passage(topic: tuple[str, ...], variant: int) -> str:
    title, domain, observation, mechanism, evidence, implication, limitation, example, _ = topic
    if variant == 0:
        body = f"{observation} {mechanism} For example, {example[0].lower() + example[1:]} {evidence} {implication} However, {limitation[0].lower() + limitation[1:]}"
    else:
        body = f"Researchers in {domain} examine a recurring pattern: {observation[0].lower() + observation[1:]} {evidence} The process is understandable because {mechanism[0].lower() + mechanism[1:]} {limitation} Even so, {implication[0].lower() + implication[1:]}"
    return f"{title}\n\n{body}"


def build_academic_reading() -> list[dict]:
    result = []
    for topic_index, topic in enumerate(ACADEMIC_TOPICS):
        title, domain, observation, mechanism, evidence, implication, limitation, example, main = topic
        for variant in range(2):
            passage = academic_passage(topic, variant)
            prefix = f"x-r-academic-{topic_index + 1:02d}-{variant + 1}"
            diff = ("intermediate", "advanced")[(topic_index + variant) % 2]
            result.extend([
                choice_item(item_id=f"{prefix}-m", section="reading", task="readAcademicPassage", difficulty=diff,
                            prompt="Which statement best expresses the main idea of the passage?", passage=passage,
                            answers=[first_upper(main) + ".", evidence, limitation, f"Researchers in {domain} no longer study this subject."], correct=0,
                            explanation="The correct choice summarizes the central pattern and its explanation rather than one isolated detail."),
                choice_item(item_id=f"{prefix}-d", section="reading", task="readAcademicPassage", difficulty=diff,
                            prompt="Which evidence or example does the passage use?", passage=passage,
                            answers=[evidence, "The passage reports that every case produces an identical result.", "The passage relies only on an unsupported personal opinion.", "The passage says the process has never been observed."], correct=0,
                            explanation="This choice restates the concrete evidence presented in the passage."),
                choice_item(item_id=f"{prefix}-q", section="reading", task="readAcademicPassage", difficulty=diff,
                            prompt="Why does the author mention a limitation near the end of the passage?", passage=passage,
                            answers=["To qualify the general explanation by identifying a condition under which it may not fully apply", "To introduce a completely unrelated topic", "To claim that the earlier evidence was invented", "To show that no further research is possible"], correct=0,
                            explanation=f"The limitation—{limitation[0].lower() + limitation[1:]}—sets a boundary on the broader pattern."),
            ])
    return result


RESPONSE_CONTEXTS = [
    ("workshop", "library", "Friday", "notes", "the original room is closed"),
    ("lecture", "auditorium", "Tuesday", "outline", "the speaker arrives later"),
    ("concert", "theater", "Saturday", "ticket", "the hall needs repairs"),
    ("meeting", "student center", "Monday", "agenda", "the adviser is unavailable"),
    ("tour", "museum", "Thursday", "map", "the west entrance is blocked"),
    ("bus", "south gate", "Wednesday", "pass", "road work blocks the usual stop"),
    ("lab", "science hall", "Friday", "form", "the equipment is being inspected"),
    ("interview", "career center", "Monday", "résumé", "the office schedule changed"),
    ("rehearsal", "music hall", "Sunday", "score", "another group needs the room"),
    ("clinic", "health center", "Tuesday", "record", "the nurse is attending training"),
    ("seminar", "Room 204", "Thursday", "article", "the larger screen is unavailable"),
    ("field trip", "north gate", "Saturday", "waiver", "the route has heavy traffic"),
    ("practice", "gym", "Wednesday", "schedule", "the floor is being cleaned"),
    ("screening", "ballroom", "Friday", "invitation", "wind makes the lawn unsafe"),
    ("session", "computer lab", "Monday", "password", "the main server is offline"),
    ("talk", "east gallery", "Sunday", "program", "the auditorium reached capacity"),
    ("appointment", "advising office", "Thursday", "degree plan", "group times no longer fit"),
    ("pickup", "parcel window", "Tuesday", "receipt", "the service counter is crowded"),
    ("class", "Room 118", "Wednesday", "worksheet", "the usual classroom is too small"),
    ("exhibit", "main gallery", "Saturday", "guide", "installation work is unfinished"),
]

RESPONSE_PATTERNS = [
    ("Could you send the {item} today?", "Sure, I'll email it this afternoon.", "No, the {place} is upstairs.", "The {item} was very detailed.", "I walk to campus."),
    ("Why was the {event} moved?", "Because {reason}.", "It lasts about forty minutes.", "I moved here last year.", "The list is alphabetical."),
    ("Is the {place} still open?", "Yes, until nine tonight.", "The door is painted blue.", "I opened the file already.", "No, I took the bus."),
    ("I thought the {event} was {day}.", "It was, but the date changed.", "That was a thoughtful answer.", "I prefer the older building.", "No, the book is mine."),
    ("Would you show me the {place}?", "Of course; it is on this map.", "The show ended yesterday.", "I completed the reading.", "It costs less in summer."),
    ("How can I confirm the {event}?", "Check the message from the organizer.", "The chair is near the window.", "Three researchers confirmed it.", "I commute twice a week."),
    ("Wasn't the {event} {day}?", "Yes, that was the original date.", "The notice board is downstairs.", "No, the essay needs a conclusion.", "It was a noticeable difference."),
    ("Where is the {event} now?", "It is in the {place}.", "The place opened years ago.", "I took the test yesterday.", "Because the schedule was printed."),
    ("Should I submit the {item} today?", "Yes, it is due this afternoon.", "The submission was interesting.", "The desk is beside the door.", "I waited about ten minutes."),
    ("The {event} plan seems confusing.", "Let's review the update together.", "I confused two samples.", "The planner works in finance.", "No, it is not a person."),
    ("I can't attend the {event}.", "Contact the organizer about another option.", "Attendance increased last year.", "The arrangement is printed in blue.", "I attended the museum Sunday."),
    ("Why register for the {event}?", "The organizer needs an accurate count.", "The register is beside the door.", "The event took two hours.", "I prefer the earlier building."),
]


def build_listening_responses() -> list[dict]:
    result = []
    for pattern_index, pattern in enumerate(RESPONSE_PATTERNS):
        for context_index, context in enumerate(RESPONSE_CONTEXTS):
            event, place, day, item, reason = context
            values = dict(event=event, place=place, day=day, item=item, reason=reason)
            audio, correct, wrong1, wrong2, wrong3 = [part.format(**values) for part in pattern]
            result.append(choice_item(
                item_id=f"x-l-response-{pattern_index + 1:02d}-{context_index + 1:02d}",
                section="listening", task="listenChooseResponse", difficulty=difficulty(pattern_index + context_index),
                prompt="Choose the most appropriate response.", audio=audio,
                answers=[correct, wrong1, wrong2, wrong3], correct=0,
                explanation="The keyed response directly addresses the speaker's question, request, correction, or concern.",
            ))
    return result


def build_conversations() -> list[dict]:
    result = []
    for index, context in enumerate(CAMPUS_CONTEXTS):
        event, old, new, reason, action, deadline, exception, exception_answer = context
        script = (f"Student: I am checking on the {event}. I had written down that it was {old}. "
                  f"Staff member: That was the original plan, but it will now {new} because {reason}. "
                  f"Student: I need to make sure I do not miss the update. What should I do? "
                  f"Staff member: {period(f'Please {action} by {deadline}')} {period(f'Also, {exception.lower()}')}")
        prefix = f"x-l-conversation-{index + 1:02d}"
        diff = difficulty(index)
        result.extend([
            choice_item(item_id=f"{prefix}-p", section="listening", task="listenConversation", difficulty=diff,
                        prompt="Why does the student speak with the staff member?", audio=script,
                        answers=[f"To clarify changed arrangements for the {event}", "To apply for a full-time staff position", "To challenge the results of a scientific study", "To request a refund for a textbook"], correct=0,
                        explanation="The student is trying to confirm how the activity's plan has changed."),
            choice_item(item_id=f"{prefix}-r", section="listening", task="listenConversation", difficulty=diff,
                        prompt="Why did the original plan change?", audio=script,
                        answers=[first_upper(reason), "The student arrived too early", "The event has no organizer", "A textbook was unavailable"], correct=0,
                        explanation="The staff member explicitly gives this reason for the change."),
            choice_item(item_id=f"{prefix}-a", section="listening", task="listenConversation", difficulty=diff,
                        prompt="What does the staff member advise the student to do?", audio=script,
                        answers=[first_upper(action), "Ignore all future messages", "Cancel every campus appointment", "Wait until after the event"], correct=0,
                        explanation="This is the action stated in the staff member's final turn."),
            choice_item(item_id=f"{prefix}-e", section="listening", task="listenConversation", difficulty=diff,
                        prompt="Who is described as an exception?", audio=script,
                        answers=[first_upper(exception_answer), "Every employee on campus", "Only visiting professors", "All students in the first year"], correct=0,
                        explanation="The closing sentence identifies the group that is unaffected or exempt."),
        ])
    return result


def build_announcements() -> list[dict]:
    result = []
    for index, context in enumerate(CAMPUS_CONTEXTS[:25]):
        event, old, new, reason, action, deadline, exception, exception_answer = context
        script = (f"Here is an update about the {event}. It was {old}; instead, it will now {new}. "
                  f"The change is necessary because {reason}. {period(f'Please {action} by {deadline}')} {period(exception)}")
        prefix = f"x-l-announcement-{index + 1:02d}"
        diff = difficulty(index + 1)
        result.extend([
            choice_item(item_id=f"{prefix}-m", section="listening", task="listenAnnouncement", difficulty=diff,
                        prompt="What is the main purpose of the announcement?", audio=script,
                        answers=[f"To explain a revised plan for the {event}", "To summarize a completed experiment", "To advertise apartments outside the city", "To introduce a new faculty member"], correct=0,
                        explanation="The announcement presents new arrangements and instructions for an existing activity."),
            choice_item(item_id=f"{prefix}-n", section="listening", task="listenAnnouncement", difficulty=diff,
                        prompt=f"What is now planned for the {event}?", audio=script,
                        answers=[first_upper(new), first_upper(old), "It will be canceled permanently", "No plan has been selected"], correct=0,
                        explanation="The announcement explicitly states the revised plan."),
            choice_item(item_id=f"{prefix}-a", section="listening", task="listenAnnouncement", difficulty=diff,
                        prompt="What are participants asked to do?", audio=script,
                        answers=[first_upper(action), "Bring a research sample", "Pay a new membership fee", "Contact the city newspaper"], correct=0,
                        explanation="This is the instruction that follows the explanation of the change."),
            choice_item(item_id=f"{prefix}-e", section="listening", task="listenAnnouncement", difficulty=diff,
                        prompt="Which group is specifically described as unaffected or exempt?", audio=script,
                        answers=[first_upper(exception_answer), "All campus employees", "Every international visitor", "Only the event speakers"], correct=0,
                        explanation="The final sentence marks this group as an exception."),
        ])
    return result


def build_academic_talks() -> list[dict]:
    result = []
    for index, topic in enumerate(ACADEMIC_TOPICS):
        title, domain, observation, mechanism, evidence, implication, limitation, example, main = topic
        script = (f"Today in {domain}, consider {title.lower()}. {observation} {mechanism} "
                  f"A useful illustration is this: {example} {evidence} {limitation} {implication}")
        prefix = f"x-l-talk-{index + 1:02d}"
        diff = ("intermediate", "advanced")[index % 2]
        result.extend([
            choice_item(item_id=f"{prefix}-m", section="listening", task="listenAcademicTalk", difficulty=diff,
                        prompt="What is the talk mainly about?", audio=script,
                        answers=[first_upper(main) + ".", evidence, limitation, "A campus policy unrelated to the academic topic"], correct=0,
                        explanation="The correct choice integrates the central phenomenon with the process described."),
            choice_item(item_id=f"{prefix}-d", section="listening", task="listenAcademicTalk", difficulty=diff,
                        prompt="What evidence does the speaker mention?", audio=script,
                        answers=[evidence, "No observations have ever been recorded", "Every case is identical regardless of conditions", "Only an anonymous opinion supports the idea"], correct=0,
                        explanation="This statement reproduces the evidence identified in the talk."),
            choice_item(item_id=f"{prefix}-i", section="listening", task="listenAcademicTalk", difficulty=diff,
                        prompt="What implication does the speaker draw?", audio=script,
                        answers=[implication, "The limitation makes the entire phenomenon impossible", "Researchers should ignore the topic's conditions", "The example proves that further study is unnecessary"], correct=0,
                        explanation="The speaker explicitly connects the findings to this broader implication."),
            choice_item(item_id=f"{prefix}-x", section="listening", task="listenAcademicTalk", difficulty=diff,
                        prompt="Why does the speaker include an illustration?", audio=script,
                        answers=["To make the described process or pattern concrete", "To replace the topic with an unrelated campus story", "To show that the evidence was collected incorrectly", "To argue that examples are never useful"], correct=0,
                        explanation=f"The illustration—{example[0].lower() + example[1:]}—makes the academic relationship easier to recognize."),
        ])
    return result


def build_sentences() -> list[dict]:
    result = []
    for index, (subject, past_a, past_b, present_a, future_b) in enumerate(BUILD_CONTEXTS):
        forms = [
            ["although", subject, past_a, "they", past_b],
            [subject, past_b, "because", "the team", past_a],
            ["before", subject, past_b, "the team", past_a],
            [subject, "not only", past_a, "but also", past_b],
            ["the report explains", "why", subject, present_a],
            ["if", subject, present_a, "the team will", future_b],
        ]
        for form_index, correct in enumerate(forms):
            item_number = index * len(forms) + form_index + 1
            shuffled = correct.copy()
            random.Random(2026000 + item_number).shuffle(shuffled)
            result.append(blank_question(
                item_id=f"x-w-sentence-{item_number:03d}", section="writing", task="buildSentence",
                difficulty=("intermediate", "advanced")[form_index % 2],
                prompt="Build a grammatical sentence using every phrase.",
                sentence_tokens=shuffled, correct_tokens=correct,
                guidance="Select every phrase in the order that forms one complete sentence.",
                sample=correct[0].capitalize() + " " + " ".join(correct[1:]) + ".",
                explanation="The keyed order creates a complete sentence with standard clause structure and logical connectors.",
            ))
    return result


def build_emails() -> list[dict]:
    result = []
    for index, context in enumerate(CAMPUS_CONTEXTS[:30]):
        event, old, new, reason, action, deadline, exception, _ = context
        prompt = (f"You planned around the {event}, which was {old}, but learned that it will now {new} because {reason}. "
                  f"Write an email to the organizer. Explain how the change affects you, ask one specific question, and propose a practical solution or next step.")
        sample = (f"Dear Organizer,\n\nI am writing about the {event}. I had planned around the original arrangement, so the update affects my schedule. "
                  f"Could you please confirm whether I should {action} by {deadline}? If another option is available, I would appreciate using it; otherwise, I will adjust to the revised plan. "
                  f"I also understand that {exception.lower()}.\n\nThank you for clarifying the arrangements.\nBest,\nA student")
        result.append(blank_question(
            item_id=f"x-w-email-{index + 1:02d}", section="writing", task="writeEmail",
            difficulty=("intermediate", "advanced")[index % 2], prompt=prompt,
            passage=f"To: {event.title()} Organizer\nSubject: Question about the revised arrangement",
            guidance="Address all three purposes. Use an informative subject, courteous tone, specific request, and clear next step.",
            sample=sample,
            explanation="A strong response explains the impact, asks a focused question, proposes a workable next step, and follows email conventions.",
        ))
    return result


def build_discussions() -> list[dict]:
    result = []
    for index, (topic, question, pro, con, compromise) in enumerate(DISCUSSION_TOPICS):
        prompt = f"Professor: {question} Explain the reasoning behind your position."
        passage = f"Nora: I support the idea because {pro}.\n\nEli: I am concerned that {con}."
        sample = (f"Both classmates identify an important consideration. I would use a balanced policy: {compromise}. "
                  f"This approach preserves the benefit Nora mentions—that {pro}—while responding to Eli's concern that {con}. "
                  "A flexible rule is more likely to achieve the educational goal without imposing the same cost on every student.")
        result.append(blank_question(
            item_id=f"x-w-discussion-{index + 1:02d}", section="writing", task="academicDiscussion",
            difficulty="advanced", prompt=prompt, passage=passage,
            guidance="State and support your own position, develop at least one reason or example, and engage with an idea already in the discussion.",
            sample=sample,
            explanation="A strong contribution advances a relevant position, connects it to the existing discussion, and develops the reasoning with clear language.",
        ))
    return result


def build_repeat_items() -> list[dict]:
    result = []
    for index, context in enumerate(CAMPUS_CONTEXTS):
        event, old, new, reason, action, deadline, exception, _ = context
        scripts = [
            f"The {event} has a revised campus schedule.",
            period(f"Please {action} by {deadline}"),
            f"Participants should note that the {event} will now {new}.",
            f"Although {reason}, the organizer has provided a practical alternative for the {event}.",
        ]
        levels = ["foundational", "foundational", "intermediate", "advanced"]
        for variant, (script, level) in enumerate(zip(scripts, levels), start=1):
            result.append(blank_question(
                item_id=f"x-s-repeat-{index + 1:02d}-{variant}", section="speaking", task="listenRepeat",
                difficulty=level, prompt="Repeat the sentence you hear.", audio=script,
                guidance="After recording, rate how accurately and intelligibly you reproduced the complete sentence.",
                sample=script,
                explanation="Listen and Repeat practice rewards accurate wording, intelligible pronunciation, and preservation of the sentence's rhythm and meaning.",
            ))
    return result


def build_interviews() -> list[dict]:
    result = []
    for index, (topic, experience, option_a, option_b, problem, trend) in enumerate(INTERVIEW_TOPICS):
        prompts = [
            (f"Let's talk about {topic}. Tell me about a time when you were {experience}. What made the experience successful or memorable?", "Describe a specific experience and explain its importance."),
            (f"For {topic}, which do you generally prefer: {option_a}, or {option_b}? Why?", "Choose one option and develop at least two relevant reasons or examples."),
            (f"Imagine that {problem}. What would you do first, and why?", "Explain a practical response and the reasoning behind it."),
            (f"A recent trend is that {trend}. What is one likely benefit and one possible challenge?", "Discuss both a benefit and a challenge, using clear supporting detail."),
        ]
        for variant, (audio, guidance) in enumerate(prompts, start=1):
            result.append(blank_question(
                item_id=f"x-s-interview-{index + 1:02d}-{variant}", section="speaking", task="interview",
                difficulty=("intermediate", "intermediate", "advanced", "advanced")[variant - 1],
                prompt="Respond to the interviewer.", audio=audio, guidance=guidance,
                explanation="Interview responses should be intelligible, fluent, relevant, developed, and use appropriate vocabulary and grammar.",
            ))
    return result


def validate(items: list[dict]) -> None:
    expected = {
        "completeWords": 360,
        "readDailyLife": 180,
        "readAcademicPassage": 180,
        "listenChooseResponse": 240,
        "listenConversation": 140,
        "listenAnnouncement": 100,
        "listenAcademicTalk": 120,
        "buildSentence": 180,
        "writeEmail": 30,
        "academicDiscussion": 30,
        "listenRepeat": 140,
        "interview": 100,
    }
    counts = Counter(item["taskType"] for item in items)
    assert counts == expected, (counts, expected)
    assert len(items) == 1_800
    ids = [item["id"] for item in items]
    assert len(ids) == len(set(ids)), "duplicate item IDs"
    fingerprints = [
        "|".join((
            item["taskType"],
            item["prompt"],
            item.get("passage") or "",
            item.get("audioScript") or "",
            " ".join(item["correctSentenceTokens"]),
        ))
        for item in items
    ]
    assert len(fingerprints) == len(set(fingerprints)), "duplicate prompt/stimulus fingerprints"
    for item in items:
        if item["choices"]:
            assert len(item["choices"]) == 4
            assert len({choice["text"] for choice in item["choices"]}) == 4
            assert item["correctChoiceID"] in {choice["id"] for choice in item["choices"]}
        if item["taskType"] == "completeWords":
            assert item["acceptedTextAnswers"]
        if item["taskType"] == "buildSentence":
            assert sorted(item["sentenceTokens"]) == sorted(item["correctSentenceTokens"])
            assert len(item["sentenceTokens"]) == len(set(item["sentenceTokens"]))
        assert item["sourceNote"]


def normalize_punctuation(items: list[dict]) -> None:
    """Remove doubled sentence stops introduced when a field ends in an abbreviation."""
    for item in items:
        for key, value in item.items():
            if isinstance(value, str):
                item[key] = re.sub(r"\.\.(?=\s|$)", ".", value)
        for choice in item["choices"]:
            choice["text"] = re.sub(r"\.\.(?=\s|$)", ".", choice["text"])


def main() -> None:
    items = (
        build_complete_words()
        + build_daily_reading()
        + build_academic_reading()
        + build_listening_responses()
        + build_conversations()
        + build_announcements()
        + build_academic_talks()
        + build_sentences()
        + build_emails()
        + build_discussions()
        + build_repeat_items()
        + build_interviews()
    )
    normalize_punctuation(items)
    validate(items)
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(json.dumps(items, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Wrote {len(items):,} validated questions to {OUTPUT}")
    for task, count in sorted(Counter(item["taskType"] for item in items).items()):
        print(f"  {task}: {count}")


if __name__ == "__main__":
    main()
