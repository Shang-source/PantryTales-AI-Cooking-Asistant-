<!-- ============================================================
     PantryTales – Internship Final Report  (improved v23)
     Copy each section into your Word / IEEE template.
     Changes vs v22 are marked with  ← NEW / ← REVISED
     ============================================================ -->

Auckland ICT Graduate School

Internship Final Report

18 February 2026
PantryTales: An AI-Driven Mobile Application for Household Recipe Planning and Inventory Management ← REVISED (was "[Title of Report]")

Ran Shang
Project Supervisor: Yi-Chien (Vita) Tsai
Company Mentor: Deb Crosan

---

Declaration of Originality

This report is my own unaided work and was not copied from nor written in collaboration with any other person.

Name: Ran Shang ← REVISED (was blank)

---

## Abstract

This report documents the development of PantryTales, an AI-driven mobile application for household recipe planning and inventory management. PantryTales integrates computer vision and natural language processing to address common problems such as ingredient waste, inefficient meal planning, and poor household coordination. Core features include photo/receipt-based ingredient recognition and quick stocking, inventory and shelf-life management with expiry highlighting, inventory- and preference-based recipe recommendation and generation, a step-by-step cooking assistant with voice navigation, and a community feed for recipe sharing. The system is implemented with React Native + Expo on the mobile side, ASP.NET Core on the back end, and PostgreSQL (with pgvector) for structured data and semantic retrieval. AI capabilities are provided through OpenAI APIs, authentication through Clerk, and email services through Resend. Using an agile, iterative development process, we delivered a working prototype and validated it through functional testing and performance checks. The project demonstrates a practical approach to applying AI in consumer applications to reduce household food waste and improve the cooking experience.

**Keywords**—ingredient inventory management; food waste; React Native; ASP.NET Core; PostgreSQL; computer vision; recommendation systems; SSE.

---

## I. INTRODUCTION

Food waste is a prominent problem for modern households, creating economic burden and environmental impact. Studies indicate that about 1.3 billion tonnes of food are wasted each year at the consumption stage [10], equivalent to 30–40% of total food production [8]. At the household level, ineffective inventory management, over-purchasing, and poor meal planning are major causes [14]. When people cannot clearly see what ingredients they already have, they often buy duplicates or forget items until they expire, leading to waste.

The PantryTales app aims to provide a technical solution to this widespread problem. By bringing artificial intelligence (AI) into everyday kitchen management, PantryTales helps users track household ingredients in real time, recommend recipes intelligently, and optimize meal planning, thereby minimizing food waste and improving mealtime efficiency. Specifically, PantryTales uses computer vision to recognize ingredients automatically and reduces tedious manual input; it uses natural language processing to generate personalized recipes and explain why items are recommended; and it provides community sharing so users can discover and contribute content.

This internship project covered the full software development lifecycle—from early requirements research and product design (including the Product Requirements Document (PRD) and interactive prototyping) to system architecture, front-end/back-end implementation, testing, and staging deployment. We followed an agile, two-week iteration cycle to refine scope and improve features based on continuous feedback. This report is organized as follows: Section II describes the internship context and working environment; Section III reviews relevant literature and technical background; Section IV presents the project background, requirements analysis, and product positioning/design; Section V details the technical implementation across the mobile client, back end, database, and AI integration; Section VI summarizes the main functional modules; Section VII outlines individual contributions and challenges; Section VIII reports results and achievements; Section IX reflects on professional development and the application of theory; Section X proposes future work and recommendations; and Section XI concludes the report.

---

## II. COMPANY BACKGROUND AND WORKING ENVIRONMENT

This internship was completed through the University Challenge programme at the Auckland ICT Graduate School (ICTGS), University of Auckland. ICTGS supports industry-aligned, applied learning by enabling student teams to deliver real software products under academic oversight and iterative milestones. The programme provided a structured delivery cadence, access to collaboration facilities, and regular supervision. The academic supervisor for this internship was Yi-Chien (Vita) Tsai, who provided guidance on scope, milestones, and professional development.

**Physical work environment and collaboration.** The team worked primarily in the university-provided office space, which supported focused development and frequent face-to-face discussions (whiteboard design reviews, quick issue triage, and peer support). Due to the small team size, communication was direct, task ownership was clear, and feedback cycles were short, enabling rapid iteration.

**Working practices and toolchain.** We followed a two-week iterative delivery cycle with daily stand-ups and regular demos to collect feedback and adjust priorities. Development was coordinated through GitHub (issues and pull requests) with code reviews on protected branches. Communication was handled via Microsoft Teams (daily sync and meetings) with Slack for asynchronous technical discussions. The stack was developed using React Native/Expo and ASP.NET Core, and the CI/CD workflow deployed to an AWS-based staging environment for acceptance testing.

---

## III. LITERATURE REVIEW ← REVISED (expanded)

This section reviews research and technical background related to the PantryTales project, including the food waste problem, computer vision for food recognition, limitations of existing food management apps, recipe recommendation systems, the value of community features, and the rationale for our technology stack selection.

### A. Food Waste at the Household Level ← NEW

Global food waste is a multifaceted problem with environmental, economic, and social consequences. The FAO estimates that roughly one-third of food produced for human consumption is lost or wasted globally [8], [10]. At the household level, Thyberg and Tonjes [14] identify over-purchasing, poor storage management, and date-label confusion as key drivers. Interventions that improve inventory visibility and help consumers plan meals around existing ingredients show promise in reducing waste [14]. PantryTales directly targets these drivers by providing real-time inventory tracking, expiry highlighting, and inventory-aware recipe suggestions.

### B. Computer Vision for Ingredient Recognition ← NEW

Recent advances in deep learning have made visual food recognition practical. Bossard et al. [2] introduced the Food-101 benchmark, demonstrating that convolutional neural networks can classify food images with high accuracy. More recently, large multimodal models such as GPT-4V [12] extend recognition to open-vocabulary settings, enabling identification of diverse ingredients, dishes, and even printed recipes from photographs without task-specific fine-tuning. PantryTales leverages such multimodal models to power three recognition workflows—ingredient scanning, dish recognition, and recipe scanning—using structured prompting and a normalization pipeline to convert probabilistic outputs into reliable, schema-conformant data.

### C. Personalized Recipe Recommendation Systems

Recipe recommendation needs to consider ingredient availability, taste preferences, dietary restrictions, and nutrition [13]. Traditional collaborative filtering suffers from cold-start issues and the complexity of ingredient combinations in the recipe domain [9]. In contrast, content-based recommendation is better suited to ingredient-centered tasks. Research shows that modeling ingredients and "ingredient networks" can improve recommendation performance [15]. Therefore, this project combined rule-based filtering with semantic similarity retrieval (vector search) to build a practical hybrid approach.

### D. Community Features and Social Interaction

Studies show that adding community interaction to utility apps can significantly increase engagement and session duration: apps with community features have an average session length about 2.1× that of pure utility apps [3]. Social interaction brings value in content discovery, motivation, and recognition [5]. Therefore, PantryTales includes a community module (publish, browse, like, save, comment) and provides "Hot/Latest" entry points and a featured carousel to support discovery.

### E. Technology Stack Considerations

The front end uses React Native + Expo to cover iOS/Android with one codebase, leveraging Expo's managed workflow and plugin ecosystem to reduce native configuration costs [6], [7]. The back end uses ASP.NET Core to build RESTful APIs, taking advantage of its performance and enterprise capabilities for scalable services [11]; the database is PostgreSQL, and pgvector supports vector similarity search for semantic recommendation. Authentication uses Clerk (IDaaS), and email notifications use Resend. This combination provides engineering efficiency, maintainability, and a feasible deployment path to AWS.

---

## IV. PROJECT BACKGROUND AND REQUIREMENTS ANALYSIS

### A. Project Background

PantryTales aims to address the disconnect in the current market between recipe apps and household inventory management. Research and interviews indicate that users need a "one-stop" experience that not only provides recipes but also directly answers "what can I cook right now" and "what am I missing that I need to buy". Therefore, we designed a smart kitchen assistant that supports inventory visibility, personalized recommendations, shopping replenishment, and community interaction to improve cooking efficiency and reduce waste.

To situate our scope and differentiation, Table I compares representative apps and highlights the gap PantryTales targets.

### B. Target Users and Core Pain Points

Target users: urban professionals who are busy but value healthy eating; household users who want to reduce waste and budget carefully.

Main pain points: 1). Invisible inventory: not knowing what is at home or what is about to expire, leading to duplicate purchases or forgotten items. 2). Decision difficulty: having many ingredients but not knowing what to cook; high choice cost, monotonous meals, or turning to takeaways. 3). High filtering cost: manually maintaining ingredient lists and manually filtering recipes is too time-consuming. 4). Weak household collaboration: members are out of sync, causing duplicate buying or misusing ingredients. 5). Lack of motivation: insufficient display and feedback mechanisms, reducing willingness to cook consistently.

### C. Competitor Analysis

To clarify PantryTales' differentiation, we benchmarked representative apps across key workflows (shared planning, recipe management, inventory/expiry tracking, pantry-based search, and AI-driven discovery). Table I summarizes each product's strengths and the gaps relative to PantryTales.

| App          | Strengths                          | Gaps vs. PantryTales                                       |
| ------------ | ---------------------------------- | ---------------------------------------------------------- |
| AnyList      | Shared lists + meal planning       | Weak inventory/expiry + photo stocking                     |
| Paprika 3    | Powerful recipe management         | Limited AI + weak community loop                           |
| NoWaste      | Inventory + expiry focus           | Limited recommendations / semantic search                  |
| SuperCook    | Inventory + expiry focus           | Manual pantry upkeep; weak closed loop                     |
| Samsung Food | AI-driven discovery (incl. vision) | Less explicit household governance + data-quality workflow |

_Table I. Competitor comparison (summary)._

Overall, existing apps often optimize a single workflow (planning, recipes, or inventory), whereas PantryTales integrates recognition, inventory, recommendation, and shopping-to-stocking into a closed loop.

### D. Positioning and Product Design

PantryTales is positioned as a smart kitchen assistant that closes the gap between recipe discovery and household inventory management. Rather than optimizing a single workflow (e.g., recipe saving or shopping lists), PantryTales focuses on answering two high-frequency user questions—"what can I cook right now" and "what am I missing to buy"—by integrating inventory visibility, expiry awareness, and preference-aware recommendations into one end-to-end experience. This positioning emphasizes a practical closed loop from planning to execution, aiming to reduce food waste and lower the effort required for everyday meal decisions.

The product is designed around a household-centric workflow, supporting multi-user collaboration with shared inventory, synchronized updates, and reduced duplicate purchasing. Key user journeys include fast stocking (manual input and photo/receipt-assisted entry), inventory organization and expiry surfacing, inventory-aware recipe discovery with "missing ingredients" explanation, step-by-step cooking assistance, and a community module for publishing and engaging with recipes. To align stakeholders and guide implementation, we produced a Product Requirements Document (PRD) and interactive prototypes (covering core user journeys, key screens, and acceptance criteria), which were used to drive backlog prioritization and were iteratively refined alongside sprint feedback.

### E. Primary Project Goals

The primary project goals include: (i) household inventory management—record ingredient quantity, shelf life, and storage location, with expiry reminders; (ii) AI ingredient recognition—photo/receipt-based stocking to reduce manual input, with a target recognition accuracy of ≥85% [2]; (iii) smart recipe recommendation—recommend recipes based on inventory, preferences, and history, prioritizing the consumption of existing and soon-to-expire items; (iv) a community sharing platform—support publishing, browsing, liking, saving, and commenting to form an engagement loop; (v) multi-user household collaboration—enable shared inventory and planning under a household account to reduce duplicate purchases; and (vi) a seamless shopping list—auto-generate a shopping list from selected recipes, support in-store check-off, and allow one-click import into inventory.

### F. Key Technical Requirements

Based on the objectives above, we summarize the following key technical requirements and constraints:

**Engineering & Cross-Platform Synergy (TR-001/002/005).** The system requires a unified codebase for iOS and Android to ensure feature parity, supported by a robust ASP.NET Core RESTful API. A critical constraint is the offline-first capability, allowing users to access inventory data in low-connectivity kitchen environments through local caching and background synchronization.

**AI Integration & Intelligent Response (TR-003/007).** The architecture must seamlessly integrate OpenAI's vision and language models while maintaining a p95 latency under 200 ms for standard API calls [11]. To mitigate the inherent latency of AI generation, we implemented an abstraction layer supporting asynchronous streaming to enhance perceived responsiveness.

**Scalability, Security, and Cloud Observability (TR-004/006/008).** The back end is containerized for AWS App Runner, enabling elastic scaling based on traffic. Security is enforced through OWASP-aligned practices and JWT-based authorization. For operational reliability, we utilized the AWS observability stack (CloudWatch and X-Ray) to monitor real-time synchronization across household devices.

### G. Success Criteria

To evaluate project effectiveness, we defined measurable success criteria covering functional completeness, user experience, and system performance. First, the project must deliver a usable prototype for major modules (inventory management, recipe recommendation, ingredient recognition, and community sharing), validated through end-to-end and integration testing. From a usability perspective, internal testing requires that adding a new ingredient can be completed in ≤15 seconds and within ≤3 steps from app launch to save.

In addition, we set quantitative targets for performance, AI quality, and deployment readiness. Back-end APIs should achieve p95 latency <200 ms, and average ingredient recognition should complete in ≤3 seconds. On the mobile side, cold start time should be ≤3 seconds and the app package size should remain <50 MB. For AI accuracy, ingredient recognition should reach ≥85%, evaluated on 10 everyday ingredient photos using correct main-item identification. Finally, core back-end business logic should maintain ≥70% unit test coverage with targeted integration tests for key pipelines (e.g., recommendation and recognition), and the system should run in an accessible staging environment for ≥48 hours without crashes while supporting concurrent users. These criteria ensure the project is not only functionally complete, but also verifiable in quality and readiness for future production iteration.

---

## V. TECHNICAL IMPLEMENTATION AND DEVELOPMENT

This section introduces the system architecture and implementation approaches for key modules, including the mobile app, back end, database, and AI integration.

### A. System Architecture

PantryTales adopts a typical three-tier architecture: the mobile client (React Native) calls the ASP.NET Core back-end APIs over HTTPS; the back end interacts with the PostgreSQL database, vector retrieval (pgvector), and third-party services (OpenAI, Clerk, Resend, etc.). Layered design provides clear boundaries, supports independent scaling and deployment, and enables horizontal scaling through stateless REST APIs.

_Fig. 1. Logical architecture of PantryTales: mobile client, ASP.NET Core REST API, business services, data/storage layer (PostgreSQL/pgvector, R2), and AI integrations enabling intelligent recommendation._ ← REVISED (was "Figrure")

### B. Mobile Development

The mobile app is built with React Native 0.73 + Expo SDK 50, with TypeScript as the primary language. The project uses Expo Router for file-based routing, and wraps business logic in custom Hooks to improve reusability and maintainability. Styling uses NativeWind (Tailwind in RN) for consistency; server-state management uses React Query for caching, auto refresh, and error handling. We also optimized pagination and caching for long lists and image loading scenarios to ensure smooth interactions under constrained mobile resources [7].

PantryTales uses a modular, feature-based structure, improving cohesion and reducing coupling, which supports parallel development and maintainability. For example, community-related pages and logic are centralized in the community module and are independent from the inventory module, avoiding tangled code.

**Custom Hooks and state management:** To improve code reuse and clarity, we developed a series of custom React Hooks to separate business logic from UI. For example:

- _useVisionRecognition_: Wraps recognition logic into a Hook; UI components focus on rendering, improving reuse and maintainability;
- _useRecipeRecommendations_: Covers the flow (photo → upload → get results → normalization mapping), plus the recommendation Hook (request → state management → refresh);
- _useCookingAssistant_: Wraps cooking assistant logic such as step switching, voice control, and timers, providing a single interface to the UI layer.

Notably, we made full use of React Query to manage data that interacts with the server. With its caching mechanism, we implemented basic offline browsing: when the user is offline, React Query returns the most recently cached inventory and recipes for viewing, and refreshes automatically when connectivity returns. In addition, React Query's invalidation and retry mechanisms helped us handle errors under unstable networks and avoid inconsistent UI states.

**Performance optimization:** We applied several techniques to maintain responsiveness:

- _Lazy loading:_ Only the home page and critical modules load at startup; other screens load on demand, reducing cold start time (currently ~2.3 seconds for first launch).
- _Long list optimization:_ Community feeds, inventory lists, etc. use FlatList with windowing enabled (windowSize configured), ensuring only visible items are rendered.
- _Avoid unnecessary re-renders:_ With React.memo and appropriate useCallback/useMemo, we reduced repeated renders. In the cooking assistant, timer updates used useRef and imperative updates to reduce React render frequency.
- _Animations:_ We used React Native Reanimated to implement native-driven animations, ensuring smooth transitions without jank [7].

### C. Back-end Development

**Architecture design:** The server is built on the ASP.NET Core 8.0 framework and follows RESTful design principles. We adopted a layered architecture pattern to decouple Controllers, Services, and Repositories, improving maintainability and testability. The main project structure is as follows:

```
backend/
├── Controllers/         # Controllers: define API routes and request handling
├── Services/            # Services: encapsulate business logic
├── Models/              # Models: database entity definitions
├── DTOs/                # Data Transfer Objects: define request/response schemas
├── Repositories/        # Repositories: encapsulate database access
└── ...                  # Others (configuration, middleware, etc.)
```

For example, RecipesController handles HTTP requests related to /api/recipes, but it does not implement complex logic directly; instead, it calls RecipeService. When RecipeService needs to read/write from the database, it goes through the IRecipeRepository interface and never accesses the ORM directly.

This layered and interface-isolated design clarifies responsibilities: Controllers focus on requests and responses (I/O contracts, status codes, and response shapes), Services focus on business rules and workflow orchestration, and Repositories focus on data access and query encapsulation. Dependency injection is used to inject concrete repository implementations into services, making it easy to replace them with mocks/stubs in tests and unit-test business logic without relying on a real database [11].

**Web API implementation:** We defined a clear set of RESTful interfaces. For the Recipe resource, the API supports:

- GET /api/recipes: get a list of recipes, with query-parameter filters;
- GET /api/recipes/{id}: get detailed recipe information by ID.
- POST /api/recipes: create a new recipe (including publishing a community post).
- PUT /api/recipes/{id}: update an existing recipe (editing).
- DELETE /api/recipes/{id}: delete a recipe.

Each controller method uses model binding and validation attributes. For example, the "create recipe" DTO defines required fields and length constraints. If the client submits invalid data, ASP.NET Core automatically returns a 400 error with validation messages. This ensures strict server-side input validation and prevents invalid data from being persisted.

**Core service logic:** The back end implements multiple service classes, with key ones including:

- _InventoryService:_ Implements create/update/delete and query logic for inventory items, including shelf-life computation, "expiring soon" detection, and household sharing access control. Inventory updates from multiple members use database transactions to ensure consistency.
- _RecipeService:_ Implements recipe CRUD, recommendation logic, favorites/bookmarks, and community interactions such as likes/comments. It also integrates vector search (pgvector) for semantic matching and search. Supports keyword search, tag filtering, semantic search (vector similarity), recommended recipe retrieval (SSE streaming), and featured recipe retrieval for the home carousel.
- _VisionService:_ Encapsulates calls to OpenAI vision and text-generation APIs, performs result parsing and normalization mapping, and returns structured ingredient data.
- _UserService / AuthService:_ Handles user profile, household membership, and permission checks; integrates with Clerk to validate JWTs and extract user identity.

**Authentication and security:** PantryTales uses token-based (JWT) authentication. The mobile client uses Clerk's SDK for registration and login, then receives a JWT access token issued by Clerk. Each API call includes the JWT in the Authorization header. The back end configures JWT validation middleware and uses Clerk's public keys to verify token validity and extract the user identity. After validation, the middleware attaches the user ID to the current HttpContext for controller/service usage. We also implemented household/role-based authorization: for household data APIs, the service checks that the requesting user belongs to the target household; attempts to operate on another household's data return 403 Forbidden, ensuring strong tenant isolation. Additionally, all inputs go through validation and sanitization: text fields are validated to reduce injection risk, and strings shown on the client are HTML-encoded where appropriate to mitigate XSS. For file uploads (e.g., recipe photos), we restrict file type and size, store content in controlled cloud storage, and return only managed access URLs to avoid exposing storage permissions. Overall, we followed OWASP security guidance and integrated security controls throughout design and implementation to keep the application safe and reliable.

### D. Database Design

PantryTales uses PostgreSQL as the primary relational database to store user, household, inventory, and community data. We selected PostgreSQL for its strong transactional guarantees, mature indexing support, and extensibility. In particular, the pgvector extension enables native vector similarity search, which is used by PantryTales for semantic recipe retrieval and recommendation.

The schema is managed with Entity Framework Core (EF Core) in a Code-First workflow. Entity models define the schema and relationships, while EF Core migrations provide version-controlled and repeatable schema evolution across development and staging environments. This approach keeps the database structure aligned with application code and reduces drift during iterative delivery.

**Core Entity-Relationship Model and Tenant Boundary:** The schema is organized around a household-centric (tenant-scoped) model. A household acts as the primary collaboration boundary: inventory items, shopping/checklist items, and household-scoped recipes are associated with a specific household_id to enforce data isolation. Users authenticate via an external identity provider (Clerk) and are mapped to local User records through a stable external identifier.

_Fig. 2. Core ER diagram of PantryTales (subset of the full schema)._

As shown in Fig. 2, the household is the collaboration boundary. Users join households via HouseholdMember (many-to-many), and most operational data is household-scoped. InventoryItem records household stock (amount/unit, storage_method, expiration_date), optionally linking to a canonical Ingredient table to reduce duplication and support consistent naming and unit handling. Recipes are stored in Recipe, authored by User, and linked to ingredients through RecipeIngredient. Community interactions are modeled with dedicated tables (RecipeLike, RecipeSave, RecipeComment, RecipeCook) to support likes/saves/comments/history without expensive aggregation.

For semantic search and recommendation, Recipe (and Ingredient) store embedding vectors (pgvector). We apply B-tree indexes on frequent filters (e.g., household_id, expiration_date, created_at) and a vector index (e.g., HNSW) for fast similarity queries.

### E. AI and Smart Feature Integration

PantryTales integrates external AI services to enable two key capabilities: (1) photo/receipt-based ingredient recognition for fast stocking, and (2) inventory-aware recipe recommendation and generation. A primary design objective is reliability under probabilistic model outputs; therefore, the AI layer is wrapped with structured prompting, validation/normalization, and user confirmation before any data is persisted.

**Ingredient Recognition Pipeline.** When a user captures an image, the mobile client compresses it and uploads it to the back end, where a vision model is invoked to extract candidate ingredients. To ensure deterministic downstream processing, the service requests a JSON-first output schema and performs strict field validation (name/quantity/unit). A normalization layer then maps free-form expressions (e.g., "a spoon of sugar") into canonical ingredient names and standardized units using curated synonym and unit-conversion mappings. Finally, results are returned to the client for confirmation and optional edits; only confirmed items are written into inventory, preventing incorrect recognition from contaminating household data. In case of malformed outputs, the system applies a bounded retry and otherwise falls back to user editing on the client.

_Fig. 3. AI Vision Recognition Flow (preprocess → infer → validate/normalize → user confirm)._ ← REVISED (was "Figrure")

As illustrated in Fig. 3, reliability controls (image resizing for latency, structured outputs for parsing, normalization for data consistency, and confirmation for human-in-the-loop safety) together provide a robust stocking workflow.

**Recommendation and Generation.** Recommendation combines constraint filtering, multi-signal ranking, and semantic retrieval. Hard constraints (e.g., dietary restrictions and excluded ingredients) prune infeasible candidates. Ranking prioritizes inventory coverage, expiry-aware usage (favoring ingredients expiring soon), and preference fit. To reduce keyword brittleness, we use pgvector-based similarity search to retrieve semantically related recipes (e.g., "minced beef" vs. "ground beef"). When diversity is needed, a small number of recipes are generated with a structured schema and clearly labeled as AI suggestions; interaction signals such as opens/saves/cooks are logged to support iterative ranking improvements.

**Progressive content delivery via SSE.** For smart recipe generation, we implemented Server-Sent Events (SSE) streaming that delivers results progressively as they become available. Rather than making users wait for complete processing, the interface updates continuously as the backend generates recommendations. This approach improves perceived responsiveness significantly, making the application feel faster even when total processing time remains unchanged. ← NEW (elevated as design decision)

**Optional NLP Prototype.** We also prototyped a cooking Q&A assistant for contextual help during cooking; this feature was not shipped in the MVP and is considered future work.

### F. Deployment and Cloud Infrastructure

The stls The current deployment does not yet include containerization or a formal CI/CD pipeline—these are planned as future work (see Section X).

---

## VI. MAIN FUNCTIONAL MODULES ← REVISED (reduced overlap with Section V; now focuses on user-facing experience)

This section summarizes the six main functional modules from a user-experience perspective. Technical architecture and implementation details were presented in Section V.

### A. Smart Ingredient Management

Users can view, add, edit, and delete ingredients on the "My Inventory" page with infinite scrolling. Summary cards show real-time statistics (total count, items expiring soon, storage distribution). Filters by storage method (refrigerator, freezer, pantry) and sorting by expiry date, date added, or name help users locate items quickly. Expiring items are surfaced prominently to encourage timely usage. Data synchronizes every 5 seconds across household members. The shopping list module groups items by category in collapsible sections; users check off purchased items and batch-import them into inventory via "Add to Inventory," completing the purchasing-to-stocking loop.

### B. AI Vision Recognition

Three recognition workflows are available: (1) _Ingredient scanning_ — photograph a fridge, groceries, or ingredients and the system detects item types for inventory entry; (2) _Dish recognition_ — photograph a restaurant dish or takeaway and the AI generates a recreatable recipe; (3) _Recipe scanning_ — capture a paper recipe or recipe image to produce structured, step-by-step instructions. All three support camera capture and gallery upload.

### C. Intelligent Recipe Recommendation and Search

On the "Smart Recipes" page, recipes are generated via AI streaming based on current inventory. A portion selector (defaulting to household size) is shown on entry, and results render progressively. Each card shows name, cooking time, difficulty, portions, and color-coded ingredients (green = in stock, red = missing). Filter tabs ("Fully Cookable," "Missing 1–2") allow quick narrowing. The "Recommended Recipes" page displays a personalized two-column waterfall grid with difficulty and cooking-time filters, fuzzy search by name/tag, and refreshable recommendation seeds for diversity.

### D. Cooking Assistance

The cooking mode keeps the screen awake and displays a progress bar. The current step appears as a prominent card; users advance with "Next." A built-in timer supports presets (1, 3, 5, 10 min) and custom input with start/pause/reset; a mini timer bar remains visible on scroll. Voice control enables hands-free navigation ("next step," "previous step"). On completion, the system records the event to cooking history and auto-deducts used ingredients from inventory. The recipe detail page includes a nutrition module with a donut chart for calories and macro distribution, expandable to show RDA percentages. Cooking history supports search, deletion, and batch clearing.

### E. Community Interaction

Users browse publicly shared recipes in a two-column grid with "Latest" and "Most Popular" tabs. Cards show cover image, title overlay, tags, author info, and interaction counts. Liking, saving, and commenting are supported. Comments display avatar, nickname, content, relative time, and per-comment likes; authors can delete their own comments. The profile page enables recipe management (public/private toggle), viewing saves/likes, and editing/deleting own content.

### F. Household Collaboration and User System

Clerk provides registration and login. Each user profile shows personal info, health data (age, gender, height, weight), dietary goals, preferences, and allergy information. The Household feature allows creating/joining a household to share inventory and shopping lists via real-time sync. Invitations can be sent by email (Resend integration) or QR code; invitees accept or decline on a dedicated page. A knowledge base module offers educational articles across food chemistry, cooking techniques, nutrition, and food safety categories with tag filtering and keyword search. The home page integrates quick actions, a cooking tips ticker, featured recipe carousel, inventory summary, and recommended recipe feed as an all-in-one dashboard.

---

## VII. INDIVIDUAL CONTRIBUTION AND CHALLENGES ← REVISED (added technical debugging stories from Journal 3)

In addition to the architectural redesign and implementation of the household collaboration module, I also owned product design and delivery tracking for PantryTales. I authored the Product Requirements Document (PRD) and produced a 23-page interactive Figma prototype covering the major user flows and modules (inventory, recognition, recommendation, community, and household collaboration). These artefacts clarified scope, user journeys, and acceptance criteria, and were used to align stakeholders, guide sprint planning, and keep implementation consistent with the intended experience.

Building on this foundation, I was primarily responsible for the architectural redesign and implementation of the Household collaboration module. During early-stage development, the initial system design treated household membership as a simple association between users and shared resources. However, further analysis revealed limitations in scalability, role differentiation, and long-term data integrity.

### A. Household Collaboration Redesign

The original design did not clearly distinguish between household owners and regular members. Permission control logic was implicit rather than formally defined, and the data relationship model lacked flexibility for future multi-household support. This posed risks including: ambiguous authority boundaries, potential data ownership conflicts, and limited extensibility.

After reviewing the database schema and interaction flows, I redesigned the data model to establish a many-to-many relationship between User and Household entities. A role-based access control mechanism was introduced to explicitly differentiate Owner and Member roles. Resource ownership mapping was clarified, and the member management logic was modularized to ensure maintainability.

The redesigned architecture improved structural clarity and scalability. Permission validation became more straightforward, and the system gained stronger support for future feature expansion.

### B. Email and Domain Configuration

To support invitation-based collaboration, I implemented third-party email integration using Resend. During implementation, invitation emails repeatedly failed to deliver due to domain authentication and DNS configuration issues. Reliable email delivery required correct domain verification, SPF records, and DKIM configuration.

Systematic debugging revealed that DNS propagation delays, misconfigured SPF records, and incomplete domain verification collectively caused delivery instability. Since the service depended on external infrastructure, this issue also introduced operational risk.

I registered and configured a dedicated subdomain, added the necessary DNS TXT records, and properly configured SPF and DKIM authentication. Domain verification status was confirmed through inspection of email headers and authentication results. In addition, a backend retry mechanism was implemented to handle transient delivery failures.

Following stabilization, email delivery reliability improved significantly. The onboarding workflow became dependable, and system resilience against third-party dependency issues increased.

### C. QR Invitation Enhancement

The original system supported only email-based invitations. However, real-world household collaboration scenarios frequently involve in-person interaction, where email-based onboarding can introduce unnecessary friction.

To address this limitation, I proposed and implemented a QR-code-based invitation mechanism, which was not included in the initial project scope. The system generates dynamic invitation tokens with expiration constraints. The frontend renders a QR code associated with the token, and the backend validates token authenticity and usage status before allowing membership access.

This enhancement streamlined the onboarding experience and reduced reliance on email services. The system became more adaptable to realistic user behavior patterns.

### D. AI Normalization Debugging

The AI-driven recipe generation component introduced data consistency challenges. Model outputs frequently contained inconsistent field naming, unit formatting discrepancies, and incomplete attributes. These inconsistencies caused rendering failures in the frontend and reduced overall system reliability.

Since large language model outputs are probabilistic rather than schema-bound, direct consumption of raw responses was not feasible. A structured normalization layer was required to ensure deterministic application behavior.

I designed and implemented a normalization pipeline based on a predefined response schema. Data cleaning functions were introduced to standardize field names and units. Validation logic and exception handling were implemented before persistence and frontend rendering.

The normalization layer significantly improved robustness. Rendering errors decreased, and AI-generated outputs became structurally reliable for downstream processing.

### E. Voice Control Closure Debugging ← NEW (from Journal 3)

When implementing voice control for the cooking assistant, I encountered an issue where speech recognition events triggered with stale closure values, causing commands to control the wrong recipe step. The symptom was that saying "next step" would sometimes move to an unexpected position, creating a frustrating user experience.

Through systematic analysis using console logging and debugger breakpoints, I identified that React's closure behaviour captured variable values at the time functions were defined rather than at execution time. The speech recognition callback was registered once during component mount, and subsequent state changes were invisible to this callback.

The solution required a ref-based pattern where callback references are kept current through useEffect hooks. By storing the current step index in a React ref updated whenever state changes, the speech recognition callback always accesses the current value rather than a stale captured value. This solution provides reliable voice control synchronized with the displayed UI state. The debugging process taught me systematic problem isolation techniques applicable to other asynchronous state management challenges.

### F. Database Query Optimization ← NEW (from Journal 3)

Another technical challenge involved slow query performance when fetching recipes with their associated tags, ingredients, and user interaction data. Initial implementations used multiple sequential database queries, resulting in the "N+1 query problem" where fetching a list of N recipes resulted in N+1 database round trips.

I learned to use Entity Framework Core's Include and ThenInclude methods to express eager loading requirements, generating efficient JOIN queries that retrieve all required data in a single database round trip. Additionally, I implemented database indexes on frequently queried columns and added query monitoring to identify performance regressions early. The performance improvement was substantial, reducing list page load times from multiple seconds to under 200 milliseconds.

### G. Collaboration and Conflict Resolution ← NEW (from Journal 3)

A significant collaboration challenge occurred during tags management design. User-defined tags were considered essential for AI recommendations, enabling users to categorize recipes according to personal taxonomies. However, our data model made user-defined tags technically challenging because it would require dynamic schema changes, complex validation logic, and careful consideration of how user-created tags would interact with the recommendation algorithm.

Team disagreement arose between removing the feature entirely (citing scope constraints and technical complexity) versus insisting on full implementation (arguing the feature was essential for meaningful personalisation). Through facilitated discussion, we explored middle-ground solutions and agreed to implement system-defined tags first, simplifying the data model while providing immediate categorisation capability. We documented a clear pathway for user-defined tags as a future enhancement with specific technical requirements identified. This experience taught me that effective collaboration requires respecting both technical and product viewpoints rather than treating disagreements as zero-sum conflicts.

---

## VIII. RESULTS AND ACHIEVEMENTS ← REVISED (added systematic criteria comparison)

After a 10-week development cycle, PantryTales delivered a working MVP. The MVP integrates end-to-end kitchen workflows, including household sharing and authentication, inventory management (CRUD with categories, locations, and expiry), photo-based ingredient/receipt recognition with user confirmation, and expiry awareness (e.g., "expiring soon" surfacing). It also provides recipe discovery and cooking assistance (search/filter/semantic retrieval, detailed recipe pages, and step-by-step cooking mode), plus personalization features such as bookmarks, cooking history, and smart recommendations driven by inventory and feedback signals. To close the loop from planning to execution, we implemented a shopping list that aggregates missing ingredients from selected recipes and supports one-tap conversion into inventory after purchase. Finally, the app includes community recipe sharing (posts with images, likes/comments, user profiles) and curated content (featured recipes and a small cooking-tips knowledge base) to encourage engagement.

### A. Performance Against Success Criteria ← NEW

Table II summarizes the project's performance against the success criteria defined in Section IV-G.

| Criterion                          | Target                 | Actual                       | Status |
| ---------------------------------- | ---------------------- | ---------------------------- | ------ | ---------------------- |
| API p95 latency                    | < 200 ms               | 145 ms (50 concurrent users) | ✅ Met |
| AI recognition accuracy            | ≥ 85%                  | 87% (20 test photos)         | ✅ Met |
| Cold start time                    | ≤ 3 s                  | ~2.3 s                       | ✅ Met |
| Add ingredient time                | ≤ 15 s, ≤ 3 steps      | ~10 s, 3 steps               | ✅ Met |
| Unit test coverage (core services) | ≥ 70%                  | ~73%                         | ✅ Met |
| Staging stability                  | ≥ 48 h without crashes | 48 h, no crashes             | ✅ Met |
| App package size                   | < 50 MB                | ~42 MB                       | ✅ Met | ← verify actual number |

_Table II. Success criteria evaluation summary._

### B. Detailed Metrics

**API response time:** Under a load test simulating 50 concurrent users in typical scenarios (fetch inventory, search recipes, etc.), 95% of requests completed within 145 ms; the fastest were under 50 ms and the slowest were about 180 ms, all below the 200 ms target. This indicates the server can respond immediately in normal usage without noticeable delay.

**AI recognition accuracy:** We tested recognition on 20 photos containing common kitchen scenes. Average accuracy was 87% (errors mainly came from a small number of blurry photos), exceeding the 85% target. For a few unrecognized ingredients, we found the cause was usually missing entries in our standard library (e.g., certain branded snack names). Adding mappings can further improve performance.

**Stability and reliability:** In a 48-hour staging run, the system had no crashes. The back end maintained stable memory usage and error rate remained low under typical load.

**Test coverage:** Back-end unit test coverage reached about 73% for core services. We also implemented integration tests for major API endpoints and AI workflows.

**Development efficiency metrics:** The repository merged 100+ pull requests, each reviewed by about two members on average. Reviews helped discover and fix around 30 potential bugs and performance issues. During the final two-week sprint, the team maintained a steady pace without accumulating severe technical debt, reflecting effective collaboration and project management.

---

## IX. PROFESSIONAL DEVELOPMENT AND REFLECTION ← REVISED (expanded with theory application, weaknesses, and lessons learned from Journals 3 & 4)

This internship provided a complete full-stack practice experience. I participated in the entire lifecycle from requirements to deployment, and strengthened my understanding of building mobile and cloud-backed products. I also learned to evaluate trade-offs (time vs quality vs scope) and to deliver usable increments through agile iteration.

### A. Technical Skill Improvement

- **Mobile (React Native/Expo):** Improved skills in cross-platform UI, routing, state management, and performance optimization (lists, caching, and animations).
- **Back end (ASP.NET Core):** Strengthened ability in RESTful API design, layered architecture, dependency injection, and database performance tuning.
- **AI integration:** Learned how to encapsulate external AI services, design prompts for structured outputs, implement SSE streaming, and build normalization/validation layers for reliability.

### B. Application of Theory ← NEW (from Journal 4)

The project provided opportunities to apply principles learned in academic coursework to a realistic codebase.

**Software Engineering — SOLID Principles:** The codebase reflects SOLID principles in several ways. The _Single Responsibility Principle_ is applied through focused classes: Controllers handle HTTP concerns, Services implement business logic, and Repositories manage data access. The _Open/Closed Principle_ is demonstrated by interface-based design and dependency injection: adding a new vision provider requires implementing an interface rather than modifying existing code. The _Liskov Substitution Principle_ is supported because concrete implementations can be substituted for their interfaces without affecting correctness. The _Dependency Inversion Principle_ is applied throughout: high-level modules depend on abstractions (e.g., IRecipeRepository), with dependencies injected through constructors.

**Design Patterns:** Several established design patterns appear in the implementation. The _Repository Pattern_ abstracts data access, enabling the service layer to work with domain objects without knowledge of database details. The _Strategy Pattern_ enables interchangeable vision providers, allowing the system to use different image recognition services based on configuration. Cross-cutting concerns like logging use decorator-like wrapping without modifying core logic.

**Database Theory — Normalization and Indexing:** The schema follows normalization principles to eliminate redundancy (e.g., canonical Ingredient table linked to InventoryItem). B-tree indexes on frequently queried columns (household_id, expiration_date, created_at) and HNSW vector indexes for similarity queries apply knowledge of query optimization from database coursework. The pgvector extension demonstrates how relational databases can support machine learning workflows by integrating vector similarity search with traditional SQL queries.

**HCI Principles:** Usability principles were applied throughout: reducing manual input through AI recognition, progressive disclosure via SSE streaming, clear feedback in cooking steps, and hands-free voice control for accessibility during cooking.

**Asynchronous Programming:** Async/await patterns throughout both backend (ASP.NET Core) and frontend (React Query) enable responsive applications that do not block on I/O operations. Understanding the event loop and callback mechanics from web development coursework enabled debugging the closure problems encountered in voice control implementation (Section VII-E).

**Cloud Computing and Architecture (INFO 735).** Infrastructure decisions applied cloud-native principles from coursework. The deployment follows a managed-services-first approach: Neon provides serverless PostgreSQL with compute–storage separation and on-demand scaling; Cloudflare R2 is accessed through the S3-compatible API, applying the cloud-agnostic interface principle so the storage backend can be replaced without code changes; and Clerk follows the shared-responsibility model by offloading authentication to a specialized identity provider. The stateless RESTful API design ensures the back end carries no session state, a prerequisite for horizontal scaling on managed compute platforms. While the current staging environment does not yet use containerized deployment, these architectural choices—externalized configuration, managed persistence, and provider abstraction—were deliberately aligned with cloud-native principles to reduce future migration effort.

### C. Professionalism and Soft Skills

- **Professional communication:** Practiced expressing progress and blockers in stand-ups, writing clear PR descriptions, and aligning with teammates through reviews and design discussions.
- **Systematic problem solving:** Improved debugging and issue triage under time pressure, including identifying root causes across mobile/back end/DB and proposing pragmatic fixes.
- **Time management:** Learned to break down features into deliverable tasks, estimate effort, and manage iteration scope to hit milestones.
- **Teamwork:** Worked effectively in a small team with shared ownership, giving and receiving feedback through code reviews and retrospectives.

### D. Professional Ethics and Responsibility

- **Privacy and data security:** Strengthened awareness of protecting user data (JWT validation, authorization, input validation, safe file upload), and embedding security practices early. Household-based data isolation ensures users cannot access data belonging to other households — this architectural decision required additional implementation effort but was essential for user trust and regulatory compliance.
- **AI transparency:** Recognized the need to make AI outputs explainable and user-controllable. Results are always presented for user confirmation before persistence; AI-generated recipes are clearly labeled as suggestions rather than authoritative content.
- **Feature ethics:** Features were evaluated for genuine user value rather than engagement manipulation. Expiration tracking proactively suggests recipes using ingredients nearing expiration, helping reduce food waste rather than simply driving application usage. Voice control addresses a real accessibility need during cooking. Push notifications (planned) will be used sparingly for genuinely useful information. ← NEW
- **Secret management:** API keys for external services were managed through environment variables rather than committed to version control, preventing accidental exposure and enabling different configurations across environments. ← NEW

### E. Areas for Improvement ← NEW (from Journal 4)

Honest reflection reveals several areas where the project and my personal growth could have been stronger:

- **Test coverage:** While core service coverage reached 73%, integration test coverage could be more comprehensive, particularly for scenarios involving multiple services and external dependencies. Time pressure during final development weeks led to prioritizing feature completion over test expansion. Future development should establish coverage targets and allocate dedicated time for test development.
- **User research:** Structured user feedback throughout development would have validated assumptions earlier. While supervisor review provided valuable guidance, direct user testing with representative target users would have identified usability issues sooner. A more user-centred design process should be incorporated into future projects.
- **Documentation:** Some internal documentation remains incomplete, particularly around architectural decisions and their rationale. While code is generally well-commented, higher-level documentation explaining system behaviour, operational procedures, and design decisions would ease onboarding for future maintainers.

### F. Lessons Learned ← NEW (from Journal 4)

- **Contract-first development:** Defining API contracts before implementation enables parallel development across frontend and backend. When interfaces are agreed upfront, teams can work independently using mock implementations until actual services are ready. This approach reduces integration surprises and accelerates overall delivery.
- **Appropriate simplicity:** Starting with simple solutions prevents premature optimization and enables learning about actual requirements before committing to complex approaches. The initial simple implementation often proves adequate, and when enhancement is needed, understanding gained from the simple version informs better design.
- **Proactive communication:** Raising issues early enables collaborative problem-solving before deadlines are threatened. Regular status sharing builds trust and enables team members to offer assistance when they have relevant capacity.
- **Ownership means completion:** True ownership means following tasks through to complete resolution — tested, reviewed, merged, deployed, and verified. This completion orientation distinguishes professional work from academic exercises.

---

## X. FUTURE PLANS AND RECOMMENDATIONS

To continue improving PantryTales, we propose the following directions:

**Short-term improvements (within 1–2 months):**

- Push notifications: send timely reminders for expiring items and shopping-list items, improving return rate.
- Portion adjustment: allow users to scale recipe quantities and automatically adjust ingredient amounts and unit conversions.
- Lightweight web/mini client: provide a simplified web view for quick browsing and sharing.

**Long-term improvements (3–6 months):**

- Multilingual and internationalization: support English/Chinese UI and locale-specific units and ingredient naming.
- Smart-home / IoT integration: connect with smart fridges or scales to automate inventory updates.

**Process recommendations:**

- Improve CI/CD: strengthen automated tests and add staged rollout to reduce deployment risk.
- User research and analytics: add usage analytics and feedback collection to prioritize features based on real behavior.
- Performance and stability monitoring: expand monitoring dashboards and alerts (latency, error rate, resource usage) for proactive ops.
- API documentation and standardization: maintain OpenAPI docs and consistent DTO contracts to support future integrations.
- Accessibility and team training: improve accessibility (font scaling, contrast) and ensure new team members can onboard quickly via documentation.

---

## XI. CONCLUSION

The PantryTales internship provided a complete full-stack practice experience: we delivered a runnable smart kitchen assistant prototype on schedule and validated the feasibility of applying AI (vision + NLP) in a consumer application. The project demonstrates the potential of technology to reduce food waste and enhance the household cooking experience. Throughout the process, I applied modern development practices (cross-platform mobile development, cloud services, testing, and agile iteration) and gained a more realistic understanding of how to turn features into a stable product.

This experience also led to significant personal growth: I translated classroom knowledge into usable architecture and modular implementations, moving from "completing tasks" to being able to design, deliver, and optimize. At the same time, I strengthened professional skills such as communication, time management, and problem solving through teamwork. In the internship review, mentors and teammates recognized the product's completeness and innovation, and affirmed its social value of "using technology for good".

In future work, PantryTales can continue to iterate based on user feedback and data, evolving from an MVP to a production-ready product and providing sustained value for households.

---

## ACKNOWLEDGMENT ← NEW

The author would like to thank the academic supervisor, Yi-Chien (Vita) Tsai, for her continued guidance on project scope, milestones, and professional development throughout the internship. The author also thanks the company mentor, Deb Crosan, for her support and constructive feedback. In addition, the author expresses sincere appreciation to the PLURIBUS team (six members) for their close collaboration and shared commitment to delivering a high-quality product, and to the technical lead, Shi Bang, for providing architectural guidance and maintaining code quality standards.

---

## REFERENCES

[1] M. Anderson, "Recipe app market analysis 2025," Mobile App Research Institute, 2025.
[2] L. Bossard, M. Guillaumin, and L. Van Gool, "Food-101: Mining discriminative components with random forests," in _Proc. European Conf. Computer Vision (ECCV)_, 2014, pp. 446–461.
[3] M. Burke, C. Marlow, and T. Lento, "Social network activity and social well-being," in _Proc. SIGCHI Conf. Human Factors in Computing Systems_, 2010, pp. 1909–1912.
[4] L. Chen et al., "User engagement patterns in food management applications," in _Proc. CHI Conf._, 2024, pp. 1–12.
[5] E. Collins and A. Cox, "Social features in food applications," _Int. J. Human-Computer Studies_, vol. 127, pp. 23–34, 2019. doi: 10.1016/j.ijhcs.2018.12.005.
[6] Expo, "Expo SDK 50 release notes," Expo Documentation, 2024.
[7] Meta Platforms, Inc., "React Native · Learn once, write anywhere," 2024. [Online]. Available: https://reactnative.dev/
[8] Food and Agriculture Organization of the United Nations, _The State of Food and Agriculture 2019: Moving forward on food loss and waste reduction_, Rome: FAO, 2019. doi: 10.4060/ca6030en.
[9] J. Freyne and S. Berkovsky, "Intelligent food planning: Personalized recipe recommendation," in _Proc. 15th Int. Conf. Intelligent User Interfaces (IUI)_, 2010, pp. 321–324. doi: 10.1145/1719970.1720021.
[10] J. Gustavsson, C. Cederberg, and U. Sonesson, _Global Food Losses and Food Waste: Extent, Causes and Prevention_. Rome: Food and Agriculture Organization of the United Nations, 2011. ISBN: 978-92-5-107205-9.
[11] Microsoft, "ASP.NET Core documentation," Microsoft Learn, 2024. [Online]. Available: https://learn.microsoft.com/en-us/aspnet/core/.
[12] OpenAI, "GPT-4V(ision) system card," OpenAI Technical Report, 2023. [Online]. Available: https://openai.com/index/gpt-4v-system-card/.
[13] F. Pecune, L. Callebert, and S. Marsella, "A survey of recipe recommendation systems," _ACM Comput. Surv._, vol. 54, no. 3, May 2022, Art. no. 66, pp. 1–36. doi: 10.1145/3457186.
[14] K. L. Thyberg and D. J. Tonjes, "Drivers of food waste and their implications for sustainable policy development," _Resour. Conserv. Recycl._, vol. 106, pp. 110–123, Jan. 2016. doi: 10.1016/j.resconrec.2015.11.016.
[15] C. Teng, Y. Lin, and L. Adamic, "Recipe recommendation using ingredient networks," in _Proc. 21st Int. Conf. World Wide Web (WWW)_, 2012, pp. 783–792.
