
const chatForm = document.getElementById("chatForm");
const messageInput = document.getElementById("messageInput");
const chatMessages = document.getElementById("chatMessages");
const loading = document.getElementById("loading");
const sendButton = document.getElementById("sendButton");

const documentInput = document.getElementById("documentInput");
const uploadButton = document.getElementById("uploadButton");
const uploadStatus = document.getElementById("uploadStatus");
const ragMode = document.getElementById("ragMode");

const aiProvider = document.getElementById("aiProvider");
const providerDescription = document.getElementById("providerDescription");
const modeStatus = document.getElementById("modeStatus");


// Markdown configuration
marked.setOptions({
    gfm: true,
    breaks: true
});


// Get selected provider
function getSelectedProvider() {
    return aiProvider.value;
}


// Update UI based on selected provider and RAG mode
function updateInterface() {

    const provider = getSelectedProvider();
    const isRagEnabled = ragMode.checked;

    if (provider === "gemini") {

        providerDescription.textContent = "Powered by Google Gemini";

        if (isRagEnabled) {
            modeStatus.textContent =
                "Gemini RAG mode: Answers will use uploaded documents.";
        } else {
            modeStatus.textContent =
                "Gemini chat mode: General AI conversation.";
        }

    } else {

        providerDescription.textContent = "Powered by Ollama (Local)";

        if (isRagEnabled) {
            modeStatus.textContent =
                "Ollama RAG mode: Answers will use locally generated embeddings and documents.";
        } else {
            modeStatus.textContent =
                "Enable RAG to use Ollama. General Ollama chat is not configured yet.";
        }
    }

    const ollamaWithoutRag =
        provider === "ollama" && !isRagEnabled;

    sendButton.disabled = ollamaWithoutRag;

    if (ollamaWithoutRag) {
        modeStatus.classList.add("warning");
    } else {
        modeStatus.classList.remove("warning");
    }

}


// Listen for model and RAG changes
aiProvider.addEventListener("change", updateInterface);
ragMode.addEventListener("change", updateInterface);


// Display messages in chat
function addMessage(message, sender, sources = []) {

    const messageElement = document.createElement("div");
    messageElement.classList.add("message", sender);

    const bubble = document.createElement("div");
    bubble.classList.add("message-bubble");

    if (sender === "assistant") {

        const html = marked.parse(message || "");

        bubble.innerHTML = DOMPurify.sanitize(html);

    } else {

        bubble.textContent = message;

    }

    messageElement.appendChild(bubble);

    // Display source documents
    if (sender === "assistant" && sources.length > 0) {

        const sourceContainer = document.createElement("div");
        sourceContainer.classList.add("source-list");

        const uniqueFileNames = [
            ...new Set(
                sources
                    .map(source => source.fileName)
                    .filter(Boolean)
            )
        ];

        uniqueFileNames.forEach(fileName => {

            const sourceItem = document.createElement("div");
            sourceItem.classList.add("source-item");

            const icon = document.createElement("span");
            icon.classList.add("source-icon");
            icon.textContent = "📄";

            const name = document.createElement("span");
            name.textContent = fileName;

            sourceItem.appendChild(icon);
            sourceItem.appendChild(name);

            sourceContainer.appendChild(sourceItem);

        });

        bubble.appendChild(sourceContainer);
    }

    chatMessages.appendChild(messageElement);

    chatMessages.scrollTop = chatMessages.scrollHeight;
}


// Open file picker
uploadButton.addEventListener("click", function () {

    documentInput.click();

});


// Upload document
documentInput.addEventListener("change", async function () {

    const file = documentInput.files[0];

    if (!file) {
        return;
    }

    const provider = getSelectedProvider();

    const uploadEndpoint =
        provider === "ollama"
            ? "/api/Document/upload-nomic"
            : "/api/Document/upload";

    const formData = new FormData();

    formData.append("file", file);

    uploadButton.disabled = true;

    uploadStatus.textContent =
        `Uploading and processing document using ${provider}...`;

    try {

        const response = await fetch(uploadEndpoint, {
            method: "POST",
            body: formData
        });

        const data = await response.json();

        if (!response.ok) {
            throw new Error(
                data.error || "Document upload failed."
            );
        }

        uploadStatus.textContent =
            `${data.originalFileName} uploaded successfully. ` +
            `${data.totalChunks} chunks indexed using ${provider}.`;

        ragMode.checked = true;

        updateInterface();

        addMessage(
            `Document **${data.originalFileName}** uploaded successfully.\n\n` +
            `- AI Provider: ${provider}\n` +
            `- Total chunks: ${data.totalChunks}\n` +
            `- Embedding dimensions: ${data.embeddingDimensions}`,
            "assistant"
        );

    } catch (error) {

        uploadStatus.textContent = error.message;

        console.error("Upload error:", error);

    } finally {

        uploadButton.disabled = false;
        documentInput.value = "";

    }

});


// Send chat message
chatForm.addEventListener("submit", async function (event) {

    event.preventDefault();

    const message = messageInput.value.trim();

    if (!message) {
        return;
    }

    const provider = getSelectedProvider();
    const isRagEnabled = ragMode.checked;

    if (provider === "ollama" && !isRagEnabled) {
        return;
    }

    addMessage(message, "user");

    messageInput.value = "";

    loading.textContent =
        provider === "ollama"
            ? "Ollama is generating your response..."
            : "Gemini is generating your response...";

    loading.classList.remove("hidden");

    sendButton.disabled = true;

    try {

        let endpoint;
        let requestBody;

        if (isRagEnabled) {

            // RAG question answering
            if (provider === "ollama") {

                endpoint = "/api/Document/ask-nomic";

            } else {

                endpoint = "/api/Document/ask";

            }

            requestBody = {
                question: message,
                limit: 5,
                scoreThreshold: 0.65
            };

        } else {

            // Normal Gemini chat
            endpoint = "/api/chat/send";

            requestBody = {
                message: message
            };

        }

        const response = await fetch(endpoint, {

            method: "POST",

            headers: {
                "Content-Type": "application/json"
            },

            body: JSON.stringify(requestBody)

        });

        const data = await response.json();

        if (!response.ok) {

            throw new Error(
                data.error ||
                data.title ||
                "Unable to get a response."
            );

        }

        // Display response
        if (isRagEnabled) {

            addMessage(
                data.answer,
                "assistant",
                data.sources || []
            );

        } else {

            addMessage(
                data.response,
                "assistant"
            );

        }

    } catch (error) {

        addMessage(
            `Sorry, something went wrong: ${error.message}`,
            "assistant"
        );

        console.error("Chat error:", error);

    } finally {

        loading.classList.add("hidden");

        updateInterface();

        messageInput.focus();

    }

});


// Initialize interface
updateInterface();
