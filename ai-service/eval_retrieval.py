from rag import search_documents

# Each case: a question, and the document that should answer it (None = nothing should match)
CASES = [
    ("What happens if nobody is home?", "missed-deliveries.md"),
    ("What if I miss the courier?", "missed-deliveries.md"),
    ("How long is my parcel kept at the pickup point?", "missed-deliveries.md"),
    ("Who pays if a parcel is sent back?", "missed-deliveries.md"),
    ("Do I need to sign for an expensive parcel?", "delivery-policy.md"),
    ("Do you deliver on Sundays?", "delivery-policy.md"),
    ("How fast is express delivery?", "delivery-policy.md"),
    ("Why is my parcel late?", "delays.md"),
    ("Can I get money back for a late express delivery?", "delays.md"),
    ("Will I be told if my shipment is delayed?", "delays.md"),
    ("What is the capital of France?", None),
    ("Recommend a good pizza recipe", None),
]


def main():
    top1_hits = top3_hits = positives = negatives_ok = negatives = 0

    print(f"{'Result':<8} {'Top score':>9}  {'Found first':<22} Question")
    for question, expected in CASES:
        documents = search_documents(question)
        sources = [d.source for d in documents]
        first = sources[0] if sources else "-"
        top_score = f"{documents[0].score:.3f}" if documents else "-"

        if expected is None:
            negatives += 1
            passed = not documents          # nothing should be found
            negatives_ok += passed
        else:
            positives += 1
            top1_hits += first == expected  # the best chunk is from the right document
            passed = expected in sources    # the right document is somewhere in the top 3
            top3_hits += passed

        print(f"{'PASS' if passed else 'FAIL':<8} {top_score:>9}  {first:<22} {question}")

    print()
    print(f"Top-1 accuracy:   {top1_hits}/{positives}  ({top1_hits / positives:.0%})")
    print(f"Hit rate (top 3): {top3_hits}/{positives}  ({top3_hits / positives:.0%})")
    print(f"Unrelated questions with no documents: {negatives_ok}/{negatives}")


if __name__ == "__main__":
    main()