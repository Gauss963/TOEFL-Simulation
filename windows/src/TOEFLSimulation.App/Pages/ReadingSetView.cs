using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TOEFLSimulation.App.Controls.Guide;
using TOEFLSimulation.Core;
using static TOEFLSimulation.App.Controls.Guide.GuideTheme;

namespace TOEFLSimulation.App.Pages;

/// <summary>
/// ReadingSetView: answers one set and, once checked, shows right/wrong, the explanation, and the question type for each
/// question. Pass <c>choose: null</c> for a read-only review.
/// </summary>
public sealed partial class ReadingSetView : UserControl
{
    private static int instances;
    private readonly ReadingDrillSet set;
    private readonly IReadOnlyList<bool>? results;
    private readonly List<List<(string ChoiceID, Border Box, RadioButton Radio)>> options = [];
    private bool building;

    public ReadingSetView(ReadingDrillSet set, IReadOnlyList<string?> choices, IReadOnlyList<bool>? results, Action<int, string>? choose)
    {
        this.set = set;
        this.results = results;
        var group = $"reading-{++instances}";
        var stack = new StackPanel { Spacing = 18 };
        stack.Children.Add(Card(VStack(10,
            Text(set.Title, Title3, FontWeights.Bold),
            PassageText(set.Body))));
        building = true;
        for (var index = 0; index < set.Questions.Count; index++)
            stack.Children.Add(QuestionCard(index, set.Questions[index], choices, choose, $"{group}-{index}"));
        building = false;
        Content = stack;
    }

    private static TextBlock PassageText(string text)
    {
        var block = Text(text, 17, selectable: true, font: Serif);
        block.LineHeight = 17 * 1.33 + 6;
        return block;
    }

    private Border QuestionCard(int index, TOEFLQuestion question, IReadOnlyList<string?> choices, Action<int, string>? choose, string groupName)
    {
        bool? isChecked = results is null ? null : index < results.Count && results[index];
        var type = question.ReadingType();
        var stack = new StackPanel { Spacing = 10 };
        UIElement? mark = isChecked is { } ok
            ? Icon(ok ? GuideIcons.CheckmarkCircleFill : GuideIcons.XmarkCircleFill, Title3, ok ? "GuideTealIcon" : "GuideCriticalIcon")
            : null;
        stack.Children.Add(SpacedRow(Text($"{index + 1}. {question.Prompt}", Headline, FontWeights.SemiBold), mark, alignment: VerticalAlignment.Top));

        var selected = index < choices.Count ? choices[index] : null;
        var rows = new List<(string, Border, RadioButton)>();
        foreach (var choice in question.Choices)
        {
            var radio = new RadioButton
            {
                GroupName = groupName,
                Content = Text(choice.Text, Body),
                IsChecked = selected == choice.Id,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                IsHitTestVisible = results is null && choose is not null,
                IsTabStop = results is null && choose is not null,
            };
            var choiceID = choice.Id;
            radio.Checked += (_, _) =>
            {
                if (!building && results is null) choose?.Invoke(index, choiceID);
            };
            var box = new Border { Child = radio };
            rows.Add((choiceID, box, radio));
            stack.Children.Add(box);
        }
        options.Add(rows);
        UpdateOptions(index, selected);

        if (results is not null)
        {
            var explanation = new StackPanel { Spacing = 6 };
            if (type is { } t) explanation.Children.Add(Text(t.Title(), Caption, FontWeights.Bold, "GuideTealText"));
            explanation.Children.Add(Text(question.Explanation, Subheadline));
            if (type is { } tip && isChecked == false)
                explanation.Children.Add(GuideMistakes.IconRow(GuideIcons.Lightbulb, "GuideSecondaryIcon", Secondary(tip.Tip()), Caption));
            stack.Children.Add(new Border { Style = Style("GuideInsetBorder"), Padding = new Thickness(10), Child = explanation });
        }
        return Card(stack);
    }

    /// <summary>Recolors a question's options after the learner picks one.</summary>
    public void UpdateOptions(int index, string? selected)
    {
        if (index >= options.Count) return;
        var question = set.Questions[index];
        foreach (var (choiceID, box, _) in options[index])
        {
            box.Style = Style(ReadingDrillSession.OptionState(question, choiceID, selected, results is not null) switch
            {
                "key" => "GuideOptionKey",
                "wrong" => "GuideOptionWrong",
                "selected" => "GuideOptionSelected",
                _ => "GuideOptionNone",
            });
        }
    }
}
