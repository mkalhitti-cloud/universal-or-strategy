# Bob API Key Usage Across Different CLIs

## How Bob API Key Works

When you use the Bob API key (`bob_prod_bob-admin_...`) with **any** CLI (Droid, Goose, etc.), here's what happens:

### What You Get:
1. **Bob's Infrastructure**: The API key routes to Bob's backend servers
2. **Model Selection**: You specify which model via the API call:
   - `fable-5` (Bob's flagship model)
   - `claude-opus-4-7`
   - `claude-sonnet-4-6`
   - `gemini-2.0-flash-exp`
   - etc.

3. **Agent/Mode Selection**: You can specify:
   - Custom modes (like `v12-engineer`)
   - Agent configurations
   - Rules and context

## Droid with Bob API

The `scripts/droid_settings.json` configuration:
```json
{
  "provider": "bob",
  "model": "fable-5",
  "api_key": "bob_prod_bob-admin_..."
}
```

This means:
- ✅ **Uses Bob's infrastructure** (your BobCoins budget)
- ✅ **Uses Fable-5 model** (Bob's model)
- ✅ **Can access Bob's custom modes** (like `v12-engineer`)
- ✅ **Same quality as Bob CLI**
- ✅ **Parallel execution capable** (stateless)

## Goose CLI with Bob API

Once Goose finishes setup, it will:
- Route through Bob's API
- Use your BobCoins budget
- Access the same models and agents as Bob CLI
- **BUT** without Bob CLI's parallel execution limitations

## Comparison Table

| Aspect | Bob CLI | Droid/Goose with Bob API | Droid with Google API |
|--------|---------|--------------------------|----------------------|
| **Backend** | Bob infrastructure | Bob infrastructure | Google infrastructure |
| **Models** | All Bob models | All Bob models | Gemini models only |
| **Agents** | v12-engineer, etc. | v12-engineer, etc. | N/A |
| **Parallel** | ❌ No (git notes conflict) | ✅ Yes (stateless) | ✅ Yes (stateless) |
| **Cost** | BobCoins | BobCoins | Google API pricing |
| **Quality** | Fable-5 | Fable-5 | Gemini 2.0 Flash |

## Your Current Setup Options

1. **Droid with Google API**: 
   - Uses Google's models (Gemini)
   - Different cost structure (Google API pricing)
   - Already working

2. **Droid with Bob API**: 
   - Would use Bob's models (Fable-5)
   - Your BobCoins budget
   - Need to test

3. **Goose with Bob API**: 
   - Same as Droid with Bob API
   - Currently setting up

## Testing Goose with Bob API

Once Goose finishes setup, test with:
```bash
goose session start "What is 2+2?"
```

If successful, we can use Goose for parallel Wave 2 execution with:
- ✅ Bob's Fable-5 model
- ✅ v12-engineer agent
- ✅ Your BobCoins budget
- ✅ Parallel execution capability

## Key Insight

**The Bob API key gives you Bob's full capabilities (models, agents, modes) through any CLI that supports it!**

The CLI is just the interface - the actual AI processing happens on Bob's servers using your BobCoins budget.