from google import genai

client = genai.Client()

interaction = client.interactions.create(
    model="gemini-3.5-flash",
    input="파이썬 초보자에게 API가 뭔지 3문장으로 설명해줘."
)

print(interaction.output_text)