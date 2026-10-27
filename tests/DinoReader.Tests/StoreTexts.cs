namespace DinoReader.Tests;

/// <summary>One free story in the Dino store (Slice 102): its fixed package id, the file stem its translations are stored under, the
/// credit line, the age range and the author the store card and filters use (Slice 112, Claude's drafts for review), the generated cover colour and the characters its speech tags must give a voice.</summary>
internal sealed record StoreStorySpec(string Id, string Title, string Stem, string Text, int ColourIndex, string Credits, int AgeMin, int AgeMax, string Author, IReadOnlyList<string> Characters, int Version, string Added);

/// <summary>
/// The free store stories (Slice 102, Decision 106): public-domain tales retold in plain words for ages 3 to 8, and original Dino Reader books, with a named
/// speaker on every line so the caster gives each character a voice (the starter rule: <c>said Name</c> or <c>Name said</c>, a bare
/// capitalised name). Pages are separated by a line holding only "---". The retellings are original to this repo (MIT); Peter Rabbit
/// is retold from Beatrix Potter's 1902 text in fifteen gentle pages (Slice 121); X Marks the Treasure! is an original Dino Reader book. The ids never change:
/// "Downloaded" and updates match on them.
/// <see cref="StoreBuilder"/> turns them into the files that go up to R2.
/// </summary>
internal static class StoreTexts
{
    public const string LionAndMouseId = "1ca50c6936cc44788b992726784e8132";
    public const string GoldilocksId = "67695905c10a46bd81aa498beab9609e";
    public const string RedRidingHoodId = "1f0f0cb207b244368b7aef26028f4ac5";
    public const string UglyDucklingId = "6f592017f08446eeae7adafa22318660";
    public const string RedHenId = "6eff8d779a9542fcafa76ae58ba1e96b";
    public const string BillyGoatsId = "32d04b935a5649088293314cc648c825";
    public const string BeanstalkId = "e2fb7f5de4ab4c4eb8bd2bb97c91accc";
    public const string ElvesId = "34efea95400343ac998e9ed99158d370";
    public const string TownMouseId = "67dfb7db7c1c418c811cfed88876327f";
    public const string PeterRabbitId = "18cab2f1fec84aad831ba12dccad5336";
    public const string TreasureId = "a02c0731b7ac475a896e9184f3575e7b";

    private const string AddedDate = "2026-10-24";
    private const string TreasureAddedDate = "2026-10-26";
    private const string RetoldAesop = "Retold from Aesop · free to share";
    private const string Traditional = "Traditional";
    private const string RetoldTraditional = "Retold traditional tale · free to share";

    public static IReadOnlyList<StoreStorySpec> All { get; } =
    [
        new(LionAndMouseId, "The Lion and the Mouse", "lion-and-mouse", LionAndMouse, 1, RetoldAesop, 4, 7, "Aesop", ["Lion", "Mouse"], 6, AddedDate),
        new(GoldilocksId, "Goldilocks and the Three Bears", "goldilocks", Goldilocks, 3, RetoldTraditional, 3, 6, Traditional, ["Goldilocks", "Papa Bear", "Mama Bear", "Baby Bear"], 7, AddedDate),
        new(RedRidingHoodId, "Little Red Riding Hood", "red-riding-hood", RedRidingHood, 3, RetoldTraditional, 4, 7, Traditional, ["Red Riding Hood", "Mother", "Wolf", "Grandma", "Woodcutter"], 6, AddedDate),
        new(UglyDucklingId, "The Ugly Duckling", "ugly-duckling", UglyDuckling, 0, "Retold from Andersen · free to share", 5, 8, "Hans Christian Andersen", ["Mother Duck", "Duckling", "Dilly", "Hen", "Swan"], 6, AddedDate),
        new(RedHenId, "The Little Red Hen", "little-red-hen", RedHen, 3, RetoldTraditional, 3, 6, Traditional, ["Hen", "Dog", "Cat", "Duck"], 7, AddedDate),
        new(BillyGoatsId, "The Three Billy Goats Gruff", "billy-goats-gruff", BillyGoats, 0, RetoldTraditional, 4, 7, Traditional, ["Little Billy", "Middle Billy", "Big Billy", "Troll"], 8, AddedDate),
        new(BeanstalkId, "Jack and the Beanstalk", "jack-and-the-beanstalk", Beanstalk, 2, RetoldTraditional, 5, 8, Traditional, ["Jack", "Mother", "Giant", "Peddler"], 6, AddedDate),
        new(ElvesId, "The Elves and the Shoemaker", "elves-and-shoemaker", Elves, 1, "Retold from the Brothers Grimm · free to share", 4, 8, "Brothers Grimm", ["Hans", "Greta", "Nib", "Nob"], 7, AddedDate),
        new(TownMouseId, "The Town Mouse and the Country Mouse", "town-mouse-country-mouse", TownMouse, 2, RetoldAesop, 4, 7, "Aesop", ["Town Mouse", "Country Mouse"], 6, AddedDate),
        new(PeterRabbitId, "The Tale of Peter Rabbit", "peter-rabbit", PeterRabbit, 3, "Beatrix Potter, 1902 (retold)", 3, 6, "Beatrix Potter", ["Mrs. Rabbit", "Peter", "Mr. McGregor", "Robin", "Flopsy"], 7, AddedDate),
        new(TreasureId, "X Marks the Treasure!", "x-marks-the-treasure", Treasure, 0, StoreBuilder.DinoReaderCredit, 3, 6, "Ashkan Sirous", ["Kathryn", "Seagull", "Izzy", "Pancake Polly", "Billy"], 5, TreasureAddedDate),
    ];

    public const string LionAndMouse = """
One hot afternoon a great Lion lay asleep in the shade of a tree. A tiny Mouse was running home through the grass with a berry for her supper.

Pitter, patter, pitter, patter, went her little feet. She did not see the Lion, and she ran right over his nose.
---
The Lion woke up with a roar. He put his big paw down beside the little Mouse.

"Who dares to wake the king of the beasts?" said Lion. "You gave me such a fright!"
---
The Mouse stood up on her back legs and held her paws together.

"Please let me go, great Lion," said Mouse. "I did not mean to wake you. If you are kind to me today, I will be kind to you one day."
---
The Lion laughed so hard that his tummy shook. "You, help me?" said Lion. "You are far too small to help a lion!"
---
But he was a kind king, and he lifted his paw. "Run home, little Mouse," said Lion.

Pitter, patter, pitter, patter, went her little feet.
---
Some days later the Lion went for a walk by the river. A yellow butterfly fluttered past his nose, and he bounced after it.

He was having so much fun that he did not look where he was going.
---
An old fishing net hung between two trees, drying in the sun. Bump! The Lion ran right into it.

The net wrapped all around him, from his nose to the tip of his tail.
---
The Lion wriggled this way and that way. But the more he wriggled, the more tangled he got.

"Grr! This is no fun at all!" said Lion. Then he gave a big, grumpy roar.
---
The Mouse heard the roar from far away. She sat up in her little nest and listened.

"That is the kind Lion," said Mouse. "He sounds stuck! Now it is my turn to be kind to him."
---
She ran through the grass as fast as her legs could go. Pitter, patter, pitter, patter, went her little feet.

"Do not worry, great Lion," said Mouse. "I am coming!"
---
The Mouse stood beside the net and looked up at the Lion.

"You came!" said Lion. "But you are so small. How can you help me?"

"Just wait and see," said Mouse.
---
The Mouse began to nibble the ropes with her sharp little teeth. Nibble, nibble, snip! Nibble, nibble, snip!

She nibbled and nibbled, and one by one the ropes came apart.
---
Soon there was a hole in the net, and the Lion stepped out. He shook his mane, and bits of string flew everywhere.

"Hooray!" said Mouse.
---
The Lion lay down in the grass, nose to nose with the little Mouse.

"Thank you, little friend," said Lion. "I am sorry I laughed at you. Now I know that even a small friend can be a great friend."

"Kind friends help each other," said Mouse with a smile.
---
That evening the Lion lay down under his shady tree, and the Mouse curled up on his big soft paw.

"Goodnight, little friend," said Lion. "Goodnight, great friend," said Mouse.

From that day on, the Lion and the Mouse were the best of friends.
""";

    public const string Goldilocks = """
Once upon a time three bears lived in a little house in the woods. There was big Papa Bear, middle-sized Mama Bear and little Baby Bear.

One morning their porridge was too hot to eat, so off they went for a walk while it cooled down. Baby Bear skipped out last and, oops, he forgot to shut the door!
---
Pitter-patter, pitter-patter, pitter-patter! Down came the rain.

"Quick, under this big tree!" said Papa Bear. So the three bears snuggled up close under the leaves and waited for the rain to stop.
---
A little girl called Goldilocks was picking flowers, and she wandered much too far from her mum's garden. Then the rain caught her too! She came to a little house, and its door was wide open.

"Hello? Is anybody home?" called Goldilocks. But nobody answered. Goldilocks knew she should wait outside.
---
But she went inside to keep dry. On the table there were three bowls of porridge.

She tried the big bowl. "Oh, this porridge is too hot!" said Goldilocks.

She tried the middle bowl. "Oh, this porridge is too cold!" said Goldilocks.

She tried the little bowl. "Mmm, this porridge is just right!" said Goldilocks. And she gobbled it all up!
---
Then Goldilocks saw three chairs.

She sat in the big chair. "This chair is too hard!" said Goldilocks.

She sat in the middle chair. "This chair is too soft!" said Goldilocks.

She sat in the little chair. "This chair is just right!" said Goldilocks. But creak, creak, crack! Uh-oh! One leg came off, and down she slid onto the soft rug.
---
Goldilocks felt sleepy, so she went upstairs. The big bed was too high. The middle bed was too lumpy. But the little bed was just right, and soon she was fast asleep.

Outside, the rain stopped. Drip, drip, drip! The three bears set off for home.
---
The three bears came home and looked at the table.

"Somebody's been eating my porridge!" said Papa Bear.

"Somebody's been eating my porridge!" said Mama Bear.

"Somebody's been eating my porridge, and it's all gone!" said Baby Bear.
---
Then the three bears looked at the chairs.

"Somebody's been sitting in my chair!" said Papa Bear.

"Somebody's been sitting in my chair!" said Mama Bear.

"Somebody's been sitting in my chair, and its leg came off!" said Baby Bear.
---
The three bears tiptoed upstairs.

"Somebody's been sleeping in my bed!" said Papa Bear.

"Somebody's been sleeping in my bed!" said Mama Bear.

"Somebody's sleeping in my bed, and here she is!" said Baby Bear.
---
Goldilocks opened her eyes, and there were three surprised bears looking at her. "Oh!" said Goldilocks, and she sat up.

"I'm so, so sorry," said Goldilocks. "I got caught in the rain, and your door was open. I ate your porridge, and I broke your chair. I should've asked first."
---
The bears looked at each other. Then Mama Bear gave Goldilocks a big, soft bear hug.

"Thank you for telling us the truth," said Mama Bear. "But next time, never ever go into a house you don't know. Find your grown-up first, okay?"

"And next time, I'll shut the door!" said Baby Bear. Everyone laughed. Ha, ha, ha!
---
Downstairs, Goldilocks picked up the little chair. "Can I help fix it?" said Goldilocks.

Papa Bear got a pot of glue. Goldilocks held the chair steady, Baby Bear held the leg, and Papa Bear stuck them together. Dab, dab, dab!
---
And guess what? Papa Bear's porridge had cooled down at last. "Mmm, this porridge is just right!" said Papa Bear.

So Mama Bear shared it out into four little bowls, and they all sat down to eat together.

Baby Bear sat on his fixed-up chair. "This chair is just right!" said Baby Bear.
---
Soon the sun came out, and a rainbow shone over the woods. So the three bears walked Goldilocks home, where her mum was waiting at the garden gate.

"Thank you, bears!" said Goldilocks. "Next time, I'll stay with my mum, and I'll never ever go into a house I don't know."

"Come back soon, with your mum!" said Baby Bear.
""";

    public const string RedRidingHood = """
Once upon a time a little girl lived with her mother at the edge of a big green wood. Her grandma had made her a bright red hood, and she wore it every single day. So everyone called her Red Riding Hood.

Her little white puppy, Button, went everywhere with her.
---
One morning Mother packed a basket of honey cakes and apples for Grandma's birthday.

"Stay on the path all the way," said Mother. "If a stranger asks you to leave the path, say no, thank you, and tell a grown-up you know."

"I stay on the path, all the way to Grandma's!" said Red Riding Hood.
---
Red Riding Hood waved goodbye, and off she went, with the basket on her arm and Button at her heels.

Tip, tap, tip, tap, went her little red boots along the path.
---
In the middle of the wood, a big grey Wolf was sitting on a log by the path. He looked lost, and his tummy was rumbling. Rumble, rumble, rumble!

"Hello," said Wolf. "I'm new in this wood, and I can't find my way."
---
"Will you come and pick blackberries with me, over there in the trees?" said Wolf. "I'm so hungry, and I've got nobody to play with."

"No, thank you," said Red Riding Hood. "I stay on the path, all the way to Grandma's!"

"Oh, all right," said Wolf, and his ears drooped a little.
---
Tip, tap, tip, tap, went her little red boots along the path.

Soon she met the Woodcutter, Grandma's friend, stacking logs by the path.

"A Wolf I don't know asked me to leave the path, so I said no, thank you," said Red Riding Hood.

"Well done for staying on the path and telling me," said Woodcutter.
---
The Woodcutter put down his logs. He took Red Riding Hood's hand, and they went back to see if the Wolf needed help.

"Hello, Wolf. Are you all right?" said Woodcutter.

"I'm lost, and I'm hungry, and I haven't got a single friend," said Wolf.

His tummy rumbled again. Rumble, rumble, rumble!
---
"It's Grandma's birthday today. Come to her party with us!" said Woodcutter. "She always says there's room for one more."

So they all walked along the path together, and the Wolf wagged his tail.

"I stay on the path, all the way to Grandma's!" sang Red Riding Hood.
---
Knock! Knock! Knock!

"Who's there?" said Grandma.

"It's me, Red Riding Hood, and I've brought some friends!" said Red Riding Hood.

Grandma opened the door wide. "Come in, come in! There's always room for one more!" said Grandma.
---
Everyone sat round Grandma's little table, and they shared the honey cakes and apples from the basket.

"Happy birthday, Grandma!" said Red Riding Hood.

The Wolf ate three honey cakes. "Mmm! Yum, yum, yum!" said Wolf. "This is the best cake in the whole wood!"
---
Grandma's spare glasses were lying on the table. "Can I try them on?" said Wolf. "Of course!" said Grandma. The Wolf put them on, and his eyes looked big and round!

"Wolf, what big eyes you have!" said Red Riding Hood.

"All the better to see you with!" said Wolf.

Ha, ha, ha! Hee, hee, hee! Everybody laughed.
---
Next the Wolf tried on Grandma's fluffy pink earmuffs from the peg by the door. They were much too small for his big pointy ears!

"Wolf, what big ears you have!" said Red Riding Hood.

"All the better to hear you with!" said Wolf.

Ha, ha, ha! Hee, hee, hee! Everybody laughed.
---
Then the Wolf smiled the biggest, happiest smile of all.

"Wolf, what a big smile you have!" said Red Riding Hood.

"That's because I'm not lost any more," said Wolf. "I've found some friends!"

And Grandma gave him a big, warm hug.
---
That night, tucked up in bed, Red Riding Hood told Mother all about her day.

"I stayed on the path, and I told a grown-up," said Red Riding Hood.

"Well done, my love," said Mother, and she kissed her goodnight. Button was already fast asleep at her feet.

From that day on, the Wolf came to Grandma's for honey cake every Sunday.
""";

    public const string UglyDuckling = """
One sunny summer morning, Mother Duck sat on her nest by the pond. Crack, crack, crack! Out popped three fluffy yellow ducklings.

But the biggest egg did not crack at all.
---
At last the big egg went crack! Out came a big grey duckling with a long neck and big flat feet.

"Oh my, what a big baby!" said Mother Duck, and she tucked him under her wing.

"Big or small, I love you all," said Mother Duck.
---
The next morning Mother Duck led her babies down to the pond, one behind the other.

Paddle, paddle, splish, splash! The big grey duckling swam the fastest of all.
---
On the bank the yellow ducklings looked at their big grey brother. Dilly, the smallest, began to giggle.

"Your neck is so long, and your feet are so big!" said Dilly. "You do not look like a duck at all."

The big grey duckling looked down at his feet. He felt a little sad.
---
Mother Duck put her wing around him. "Dilly, that was not kind," said Mother Duck. "Everyone is different, and that is a good thing."

"Big or small, I love you all," said Mother Duck.

Dilly hung her head. "Sorry, big brother," said Dilly. "You are the best swimmer of us all."
---
"But if I do not look like a duck, what kind of bird am I?" said Duckling.

"Go and look around the farm, and you may find out," said Mother Duck. "Be home in time for supper."

So off he waddled across the meadow, waving goodbye.
---
In the farmyard a plump brown Hen was scratching for corn.

"Am I a hen like you?" said Duckling.

"Cluck, cluck! Can you lay an egg?" said Hen. Duckling shook his head.

"Then you are not a hen," said Hen, and she smiled. "But you are a very polite little bird. Now run home for your supper."
---
Soon it was autumn, and the leaves turned red and gold. One day three big white birds with long necks like his flew high over the pond. "Those are swans," said Mother Duck.

"Am I a swan like you?" called Duckling. But the swans were too high to hear him, and they flew far away.
---
Then winter came. Snow fell softly, and the pond turned to ice.

Hen fluffed up her feathers at the barn door. "Come inside, all of you!" said Hen. "It is warm and dry in the barn."

So the ducks waddled through the snow, one behind the other, into the barn.
---
All winter long the ducks and Hen snuggled together in the soft, warm straw. Duckling slept between Mother Duck and Dilly.

"Big or small, I love you all," whispered Mother Duck.
---
At last spring came, and the ice melted away. Duckling had grown big over the winter. He went down to the pond and looked into the water.

There he saw a beautiful white bird with a long, graceful neck. "Is that really me?" said Duckling.
---
Swoosh! Three white swans glided down onto the pond and swam up to him.

"Am I a swan like you?" said Duckling.

"Yes, you are!" said Swan, and he bowed his long neck. "You are a young swan, and a fine one. Welcome, friend!"
---
Mother Duck and the ducklings came paddling out to see. "Will you swim with my family too?" said Duckling.

"Of course," said Swan. "The pond is big enough for everyone."

Paddle, paddle, splish, splash! Ducks and swans swam round the pond together.
---
That evening the sun went down, pink and gold. Dilly and the other ducklings rode home on their big brother's back.

"You are a swan, and you are still our brother," said Dilly.

"Big or small, I love you all," said Mother Duck. And Duckling was happier than he had ever been.
""";

    public const string RedHen = """
Once upon a time a little red Hen lived on a farm with her three friends, Dog, Cat and Duck.

Dog liked to snooze in the sun with his nose on his paws. Cat liked to sit on the fence and lick her paws. Duck liked to splash in his puddle.

But Hen was always busy, busy, busy!
---
One morning Hen found some grains of wheat in the yard. "Who will help me plant this wheat?" said Hen.

"Not me!" said Dog. "Not me!" said Cat. "Not me!" said Duck.

"Then I'll do it myself," said Hen.
---
So she did it all by herself. Hen dug a long, straight row in the field and dropped in the grains, one by one.

Dig, dig, pat! Dig, dig, pat!

Over by the fence, Dog, Cat and Duck were fast asleep.
---
All summer long the sun shone and the rain fell, and the wheat grew tall and golden. "Who will help me cut the wheat?" said Hen.

"Not me!" said Dog. "Not me!" said Cat. "Not me!" said Duck.

"Then I'll do it myself," said Hen.
---
So she did it all by herself. Snip, snip, snip! Snip, snip, snip! Hen cut the wheat and tied it up in neat golden bundles.

Dog, Cat and Duck watched from the shade of the apple tree.
---
Hen piled the bundles into her little red cart. "Who will help me take the wheat to the mill?" said Hen.

"Not me!" said Dog. "Not me!" said Cat. "Not me!" said Duck.

"Then I'll do it myself," said Hen.
---
So she did it all by herself. Hen pulled her cart up the hill to the windmill.

Whoosh, whoosh, whoosh, went the big sails, and soon Hen had a sack of soft white flour.
---
Hen brought the flour home to her cosy kitchen. "Who will help me bake the bread?" said Hen.

"Not me!" said Dog. "Not me!" said Cat. "Not me!" said Duck.

"Then I'll do it myself," said Hen.
---
So she did it all by herself. Hen mixed the flour with water and squished it into a big round loaf.

Mix, mix, squish! Mix, mix, squish!

Dog, Cat and Duck snoozed on the rug in a sleepy heap.
---
At last the bread was ready, round and golden. Sniff, sniff, sniff! The smell of warm, fresh bread floated all over the farm. Dog, Cat and Duck woke up and ran to the table.

"Who will help me eat the bread?" said Hen.

"I will!" said Dog. "I will!" said Cat. "I will!" said Duck.
---
Then Dog looked at the bread, and he looked at Hen. "You did all the work by yourself," said Dog.

"And we didn't help you at all," said Cat.

"We're sorry, Hen," said Duck. "Next time we'll help. We promise!"
---
Hen gave them a big smile. "Thank you for saying sorry," said Hen. "Bread is yummier when we share it."

She broke the loaf into four pieces, and they had a picnic under the apple tree. Munch, munch, munch! Yum, yum, yum!
---
After the picnic there was a big pile of cups and plates. This time everyone wanted to help!

Dog washed, Cat dried, and Duck splashed in the bubbles. Splish, splash, splosh! Hen laughed and laughed.

"Washing-up is fun when we do it together!" said Duck.
---
The next spring Hen found some more grains of wheat. "Who will help me plant this wheat?" said Hen.

"I will!" said Dog. "I will!" said Cat. "I will!" said Duck.

They already had their little spades ready.
---
So the four friends planted the wheat together, side by side. Dig, dig, pat! Dig, dig, pat! They dug and planted and laughed until the sun went down.

"Helping is more fun together," said Hen. And Dog, Cat and Duck all agreed.
""";

    public const string BillyGoats = """
Once upon a time, three Billy Goats lived on a hill. They were the Billy Goats Gruff. Little Billy was the smallest, Middle Billy was a bit bigger, and Big Billy was the biggest of all.

Uh-oh! The grass on their hill was nearly all gone. But across the river there was a meadow full of yummy green grass.
---
To get to the meadow, the goats had to cross a little wooden bridge. And under the bridge lived a grumpy Troll, with eyes as big as saucers and a nose as long as a carrot.

He sat there all by himself, day after day.
---
Little Billy went first. Trip, trap, trip, trap, went his little hooves on the bridge.

"Who's that trip-trapping over my bridge?" said Troll.

"It's only me, Little Billy Goat Gruff," said Little Billy in a teeny-tiny voice. "I'm going to the meadow to munch some grass."
---
"Nobody crosses my bridge without a stamping match!" said Troll.

"Oh, please let me cross," said Little Billy. "I'm too little to stamp. Wait for my big brother. He's much bigger than me!"

"Oh, all right then, off you go!" said Troll. And Little Billy skipped across the bridge to the meadow.
---
Next came Middle Billy. Trip, trap, trip, trap, went his hooves on the bridge.

"Who's that trip-trapping over my bridge?" said Troll.

"It's only me, Middle Billy Goat Gruff," said Middle Billy. "I'm going to the meadow to munch some grass."
---
"Nobody crosses my bridge without a stamping match!" said Troll.

"Oh, please let me cross," said Middle Billy. "I'm too little to stamp. Wait for my big brother. He's much bigger than me!"

"Oh, all right then, off you go!" said Troll. And Middle Billy trotted across the bridge to the meadow.
---
Last of all came Big Billy. TRIP, TRAP, TRIP, TRAP, went his big hooves on the bridge. The whole bridge shook and shook!

"Who's that trip-trapping over my bridge?" said Troll.

"It's me, Big Billy Goat Gruff," said Big Billy in a big, deep voice.
---
"Nobody crosses my bridge without a stamping match!" said Troll, and he stomped up onto the bridge.

Stamp, stamp, stamp, went his big bare feet. Big Billy stood very still and waited for his turn.
---
But the old boards were wet and slippery. Whoops! The Troll wibbled and wobbled, this way and that way.

Splash, splash, splash! He slid off the low bridge and sat down in the shallow river.
---
"Are you okay?" said Big Billy.

The water was nice and cool, and it only came up to the Troll's tummy. A little fish tickled his toes, and he started to giggle.

"Ho, ho, ho! That tickles!" said Troll. And the three Billy Goats laughed too.
---
The soggy Troll climbed out onto the bank at the bottom of the hill and sat down. Big Billy came down off the bridge and sat next to him.

"Why are you so grumpy?" said Big Billy. "I think you're just lonely."

"Nobody ever asks me to come along," said Troll.
---
"Come across with us and try the yummy green grass," said Big Billy.

So Big Billy and the Troll walked across the bridge, side by side, to the other goats in the meadow.
---
The Troll nibbled a little bit of grass. Mmm, mmm, mmm! It was so yummy that he gave a great big, happy smile.

"Thank you, Billy Goats," said Troll. "This is the best day ever!"
---
After that, the three Billy Goats Gruff and their new friend munched yummy green grass all summer long.

Every evening the goats went trip, trap, trip, trap, home over the bridge, and the Troll waved goodnight.

"Come back tomorrow!" said Troll.

Snip, snap, snout, this tale's told out.
""";

    public const string Beanstalk = """
Once upon a time a boy called Jack lived in a little cottage with his mother and their old cow, Milky-White. They didn't have much, and the cupboard was nearly empty.

"Here's our last coin, Jack," said Mother. "Please take it to the market and buy some seeds for our garden."
---
At the market Jack met a funny little Peddler. She had a cart full of seeds and a sleepy grey donkey.

"I've got something better than seeds," said Peddler. "Five magic beans! Plant them tonight, and look out of your window in the morning."

So Jack gave her his coin, and she gave him the beans.
---
That evening Jack showed his mother the five beans.

"Only five beans?" said Mother, and she laughed. "Well, beans are seeds too. Let's plant them and see!"

So they planted the beans under the kitchen window: one, two, three, four, five. Milky-White watched over the fence.
---
In the morning Jack and his mother ran out into the garden. "Wow!" said Jack.

A great big beanstalk had grown in the night. It went up, up, up, right into the clouds!

Even Milky-White looked up. Moo, moo, moooo!
---
Then the leaves began to rustle. Rustle, rustle, rustle! Down the beanstalk, leaf by leaf, came a plump little hen with shiny golden feathers.

She hopped onto Jack's arm. Cluck, cluck, cluck!

"Hello, little hen," said Jack. "Are you lost?"
---
"I think she lives up in the clouds," said Jack. "Somebody up there must be missing her. Mother, may I take her home?"

"Let's take her home together," said Mother. She tied a basket onto Jack's back and tucked the little hen inside.

"Hold on with both hands, one step at a time," said Mother.
---
Jack held on to the beanstalk with both hands, and Mother climbed right behind him.

"Hold on with both hands, one step at a time, Mother," said Jack. Up and up they climbed, slowly and carefully, past the treetops and the birds, right into the soft white clouds.

The little hen peeked out of the basket. Cluck, cluck, cluck!
---
At the top, a road of soft white cloud led to a huge castle. Boom! Boom! Boom! Out came a Giant with a big, round, friendly face.

"Fee, fi, fo, fum! Hello, hello, everyone!" sang Giant, so loudly that the birds flew away. Jack giggled and put his fingers in his ears. So did Mother.
---
Then the Giant saw the little hen in Jack's basket. "Goldie!" said Giant. "My Goldie! I looked for you everywhere!"

Jack lifted Goldie out, and she fluttered up into the Giant's big hands. Cluck, cluck, cluck!

"Thank you, thank you!" said Giant. His happy voice was so loud that the castle windows rattled.
---
The Giant sat down on his castle step and gave a great big sigh. Whoosh... whoosh! The sigh blew Jack's cap right off his head.

"Oops, sorry!" said Giant, and he caught the cap between two fingers. "Everything about me is too big and too loud. When I say hello, the birds fly away, and nobody ever comes to play."
---
"I can help," said Jack. "Put a finger on your lips, like this, and sing as softly as a mouse."

The Giant put a finger on his lips. "Fee, fi, fo, fum! Hello, hello, everyone!" whispered Giant.

This time the birds didn't fly away. Two little bluebirds flew down and sat on his shoulder.
---
"Thank you, Jack," said Giant. "You brought Goldie home, and you taught me to sing softly. Goldie lays a golden egg every morning, and I'd like to share."

Very gently, the Giant put a shiny golden egg into Jack's basket.

"Come and visit me every Sunday, both of you," said Giant.
---
Jack and Mother waved goodbye and began to climb down.

"Hold on with both hands, one step at a time, Mother," said Jack. Down and down they went, slowly and carefully, with the golden egg safe in his basket.

"Goodbye, my friends!" whispered Giant, and he waved from the edge of the cloud.
---
Jack and Mother stepped off the last leaf. The Peddler was waiting at the bottom.

Jack held up the golden egg. "It's a present from our new friend, the Giant," said Jack. "Now we can buy seeds for the whole garden!"

"I told you they were magic beans," said Peddler, and she winked. Mother gave Jack a big hug.
---
That night Mother tucked Jack into bed and kissed him goodnight. From high up in the clouds came a soft little song.

"Fee, fi, fo, fum! Goodnight, goodnight, everyone!" sang Giant, as softly as a mouse.

"Goodnight, Giant," whispered Jack, and soon he was fast asleep. From that day on, Jack and the Giant were the best of friends.
""";

    public const string Elves = """
Once upon a time, a shoemaker called Hans lived with his wife Greta, right above their little shoe shop. A sleepy ginger cat snoozed by the door.

Hans and Greta made good, strong shoes, and they were kind to everyone who came in.
---
One snowy evening, Hans and Greta were very, very tired. Oh dear, there was only enough leather left for one more pair of shoes.

Hans cut it out and put the pieces on the workbench. "We'll sew them in the morning," said Hans, with a great big yawn.
---
That night, while Hans and Greta were fast asleep upstairs, a happy little sound came from the workshop downstairs.

Tap, tap, tap! Stitch, stitch, stitch!
---
In the morning Hans came downstairs. And there on the workbench was a pair of shiny new shoes, with teeny-tiny, perfect stitches!

"Greta, come and look!" said Hans. "Who made these?"

"Oh, wow! What beautiful shoes!" said Greta.
---
That same morning, a lady in a purple coat came into the shop. She tried on the new shoes, and they fit just right.

She loved them so much, she paid a really good price for them. Now Hans could buy leather for two more pairs!
---
After that, every evening Hans cut out more leather and left it on the workbench. And every night the happy little sound came back. Tap, tap, tap! Stitch, stitch, stitch!

Every morning there were more new shoes, and soon the little shop was full of happy customers.
---
One evening Greta looked at the new leather on the workbench, all cut out and ready.

"Someone's being so kind to us," said Greta. "Let's stay up tonight and see who it is. Then we can say thank you!"

"What a lovely idea," said Hans.
---
So that night Hans and Greta didn't go to bed. They snuggled up under a blanket in the big, comfy armchair in the corner, with the cat on their laps.

The moon shone in on the workbench. They waited, and waited, and waited. Hans tried really hard not to yawn.
---
At midnight two tiny elves came skipping in through the cat flap. Nib wore a pointy red hat, and Nob wore a pointy green hat.

Their little feet were bare, and their clothes were thin and full of patches.
---
Nib and Nob hopped up onto the workbench and got to work, quick as a wink.

"Tap, tap, tap!" sang Nib, with her tiny hammer.

"Stitch, stitch, stitch!" sang Nob, with his tiny needle.

Hans and Greta watched from the armchair and smiled.
---
When the very last shoe was done, Hans and Greta came over to the workbench.

"Thank you, little friends," said Greta. "You've helped us so much!"

Nib and Nob looked up and giggled. "We love making shoes for kind people," said Nib.

"See you tomorrow night!" said Nob, and he waved.
---
The next day Greta sewed two tiny warm coats and two woolly scarves. Hans made two pairs of tiny boots, the smallest boots he'd ever made!

"Now their little toes will be warm and cosy," said Greta.

"It's our turn to help them," said Hans.
---
That night the presents sat waiting on the workbench. Hans and Greta waited in the armchair. At midnight Nib and Nob came skipping in through the cat flap.

"Look, Nob! Presents!" said Nib.

"Are they for us?" said Nob.

"Yes, they're all for you!" said Greta.
---
Nib twirled round in her new red coat, and Nob stamped his tiny new boots. Stamp, stamp, stamp! They danced round and round on the workbench.

"Our toes are warm and cosy!" said Nib. "Thank you, Hans! Thank you, Greta!" said Nob.
---
"Now we're warm, we can go and help other kind people too," said Nib. "Goodbye, Hans! Goodbye, Greta!" said Nob.

Hans and Greta waved from the door, and the elves skipped away over the snow in the moonlight.

"Goodbye, little friends!" said Greta. "When someone is kind to you, you can be kind to someone else."
""";

    public const string TownMouse = """
Once upon a time, Country Mouse lived in a cosy hole under an old oak tree, at the edge of a quiet field. His best friend, Town Mouse, lived far away in a big, busy town.

Every morning Country Mouse sat in the sunshine and listened to the birds. Tweet, tweet, tweet!
---
One sunny morning, Town Mouse came to visit. She skipped across the field with her little pink bag.

Country Mouse ran out and gave her a big hug. "Welcome to my home!" said Country Mouse.
---
Country Mouse set out lunch on a flat stone: seeds, sweet blackberries and a crunchy hazelnut.

Town Mouse had never tasted a blackberry before. "Yum, yum, yum!" said Town Mouse.
---
After lunch, the two friends lay in the long grass and watched the clouds float by. Swish, swish, swish, went the wind in the grass.

Town Mouse wriggled and wriggled. "It's so quiet here!" said Town Mouse.

"Isn't it lovely?" said Country Mouse.
---
That evening the sun went down, pink and gold. Chirp, chirp, chirp, sang the crickets.

"Your home is lovely, but I love mine," said Town Mouse. "It's a bit too quiet for me. Come and stay at my house tomorrow!"

"Yes, please!" said Country Mouse.
---
The next morning, the two friends rode to town on the back of the farmer's hay cart, snug in the soft hay.

The big brown horse pulled them all the way. Clip-clop, clip-clop, clip-clop!
---
Town Mouse lived in a tall house in the middle of the town. Her home was a tiny room behind the kitchen wall, with a round red door just her size.

"Welcome to my home!" said Town Mouse.
---
The family had just finished breakfast. Town Mouse led Country Mouse up onto the big kitchen table. There were crumbs of cake, bits of cheese and a few juicy grapes.

"Help yourself!" said Town Mouse.

"Yum, yum, yum!" said Country Mouse.
---
Clatter, clatter, clatter, went the plates in the sink. The cook was washing up and humming a happy song.

Country Mouse jumped. "What's that noise?" said Country Mouse.

"It's only the plates," said Town Mouse. "Town is always busy!"
---
Then the two friends went into the hall. Vroom, vroom, vroom! A big round hoover rolled past them, all by itself.

Country Mouse put his paws over his ears. "What's that noise?" said Country Mouse.

"It's only the hoover," said Town Mouse. "Town is always busy!"
---
Bang, bang, bang! The wind was banging the big front door. Country Mouse jumped so high that his straw hat flew off his head.

"It's only the door," said Town Mouse. "Town is always busy!"

Country Mouse began to giggle. "Town is a bit too busy for me!" said Country Mouse.
---
That evening, the two friends sat on the kitchen windowsill and watched the town lights twinkle.

"Your home is lovely, but I love mine," said Country Mouse. "I like it nice and quiet."

"And I like it nice and busy!" said Town Mouse. Then the two friends laughed and laughed.
---
The next morning, Country Mouse rode home on the farmer's hay cart. Clip-clop, clip-clop, clip-clop!

Town Mouse waved from the doorstep. "Come back soon!" said Town Mouse.

"You come and visit me too!" said Country Mouse.
---
That night, Country Mouse curled up in his cosy bed under the old oak tree. Swish, swish, swish, went the wind in the grass.

"Home, sweet home," said Country Mouse. He smiled a sleepy smile and closed his eyes.
---
Far away in the busy town, Town Mouse snuggled into her own little bed. Hum, hum, hum, went the town outside her window.

"Home, sweet home," said Town Mouse.

Every summer and every winter, the two friends visited each other. And they stayed best friends for ever and ever.
""";

    public const string PeterRabbit = """
Once upon a time there were four little rabbits, and their names were Flopsy, Mopsy, Cotton-tail and Peter. They lived with their mum, Mrs. Rabbit, in a cosy burrow under a big fir tree.

Peter was the littlest. He had a brand-new green jacket and two little red shoes.
---
One morning Mrs. Rabbit picked up her basket. "I'm going to the shops," said Mrs. Rabbit. "You can play in the field, or pick blackberries down the lane."

"But stay out of Mr. McGregor's garden!" said Mrs. Rabbit. "It's his garden, not ours."
---
As soon as their mum had gone, Flopsy, Mopsy and Cotton-tail ran down the lane to pick blackberries.

But Peter didn't go with them. He wanted the yummy vegetables! He hopped the other way, straight to Mr. McGregor's garden. Lippity, lippity, lippity!
---
Mr. McGregor's garden had a big green gate, with a little gap underneath. Peter squeezed under it. Wiggle, wiggle, wiggle!

On the gate post, two little sparrows shook their heads. But Peter didn't look back.
---
The garden was full of yummy things. Munch, munch, munch! First Peter ate some lettuce. Then he ate some crunchy beans. Then he ate some radishes, one, two, three!

"Yum, yum, yum!" said Peter.
---
Soon Peter's tummy felt very full, and very funny. "Ooh, my tummy," said Peter. "I ate too much!"

He felt very hot, so he hung his jacket on a bean pole and put his shoes beside it. Then he lay down for a rest in the cool shade of a big rhubarb leaf.
---
After his rest, Peter hopped off to find the gate, and he forgot all about his jacket and shoes. But the bean plants were taller than Peter, and every path looked the same.

"Which way is the gate?" said Peter. A little mouse ran by with a big pea in her mouth. She couldn't say a word, so she just shook her head.
---
Peter sat down beside a watering can. He felt very small, and a little bit lost. Then he remembered what his mum had said that morning.

"But stay out of Mr. McGregor's garden!" said Mrs. Rabbit.

"Oh dear," said Peter. "I should have listened to my mum."
---
Creak, creak, creak! Along the path came Mr. McGregor with his wheelbarrow.

"Who's been nibbling my lettuces?" said Mr. McGregor. "Grumble, grumble, grumble!"

Peter hid behind a big cabbage and kept as still as a stone.
---
By the bean poles, Mr. McGregor found a little green jacket and two little red shoes.

"Well, well, well!" said Mr. McGregor. "Just the right size for my scarecrow!"

So he dressed his scarecrow in them. And for the first time all day, Mr. McGregor smiled.
---
When Mr. McGregor had gone in for his lunch, a robin with a round red tummy hopped down beside Peter.

"Please, can you help me find the gate?" said Peter.

"Tweet, tweet, tweet! Of course I can," said Robin. "Follow me. I know the way!"
---
Robin flew from bush to bush, and Peter hopped along behind him. Lippity, lippity, lippity!

They went past the carrots, past the onions, and past the scarecrow in its little green jacket. But the scarecrow was much too tall, and Peter was much too small.

"Bye-bye, jacket! Bye-bye, shoes!" said Peter.
---
At last, there was the big green gate! Peter squeezed under it. Wiggle, wiggle, wiggle!

"Thank you, Robin!" said Peter.

"Hop straight home now," said Robin, and he waved his wing.
---
Peter hopped all the way home to the big fir tree. Lippity, lippity, lippity! Mrs. Rabbit gave him a big hug.

"Peter! Where are your jacket and shoes?" said Mrs. Rabbit.

"I went into Mr. McGregor's garden," said Peter. "I'm sorry, Mum. Next time I'll listen."
---
Peter's tummy still felt funny, so Mrs. Rabbit tucked him into bed with a little cup of camomile tea.

Flopsy, Mopsy and Cotton-tail had bread and milk and blackberries for supper. "We saved you some blackberries for tomorrow, Peter," said Flopsy.

"Goodnight, my little bunnies," said Mrs. Rabbit. And soon Peter was fast asleep.
""";

    // Book 1 of the Captain Kathryn and Seagull series, an original story. The captain is spelled "Kathryn" so the English voices say
    // KATH-rin: Kokoro has no lexicon entry for it and espeak en-us reads it /kˈæθɹɪn/ ("Kaatrin", the old spelling, came out KAA-trin), and the possessive /kˈæθɹɪnz/.
    // Art record: docs/design/story-art/x-marks-the-treasure.md.
    public const string Treasure = """
Seagull flew high, high up over the sea. Down below she could see little boats, a sandy beach and a sunny harbour.

Then she spotted a big pirate hat she knew. It was Captain Kathryn! She was running to her ship, waving a piece of paper. She looked so happy!
---
Down, down, down swooped Seagull, and she landed right on top of Kathryn's big hat.

"Squawk! Squawk! Squawk! What's up, Kathryn?" said Seagull.

Kathryn held up an old map. It had a little island on it and a big red X. "Look! I've found the map to the lost treasure!" said Kathryn. "X marks the treasure!"
---
Kathryn ran up onto her ship. "Ahoy, pirates! Up with the sail! We're going to find the lost treasure!" said Kathryn.

Her crew came running. Izzy looked after the map and the compass. Pancake Polly made the yummiest pancakes ever. And Billy could climb right up to the top of the mast.
---
Seagull flapped her wings. "A treasure hunt? Ooh, I love treasure hunts! Can I come too?" said Seagull.

"Hooray! Of course you can! Sit on my hat till we find the treasure," said Kathryn.
---
Izzy, Pancake Polly and Billy danced a happy pirate dance all across the deck.

"Yo ho, yo ho!" sang Izzy.

"Yo ho, yo ho!" sang Billy.

"We're going to find the lost treasure!" sang Pancake Polly.
---
Billy pulled and pulled on the rope, and up went the big sail. Flap, flap, flap!

Kathryn turned the ship's big wheel. "Off we go to Treasure Island!" said Kathryn. And away they sailed, far, far across the sea.
---
The trip was long, long, long. After a while, everyone on deck felt slow and grumbly, and nobody knew why.

"Billy, can you see an island?" said Kathryn.

Billy was up in the basket at the top of the mast. He was too hungry to answer, so he just gave a big yawn. His tummy went rumble, rumble, rumble!
---
"Izzy, can you check the map with your compass?" said Kathryn.

Izzy sat on a barrel with the map on her lap. But she was hungry too, so she didn't answer. She just gave a big sigh. Her tummy went rumble, rumble, rumble!
---
"Hmm, I'll go and see what's wrong!" said Seagull, and she flew all around the ship.

Every tummy went rumble, rumble, rumble! Even Kathryn's tummy! "Squawk! Squawk! Squawk! I know! Everybody's hungry!" said Seagull.
---
Seagull flew down the steps to the ship's kitchen. And there was Pancake Polly, stirring pancake mix in her big blue bowl.

"Polly, Polly! Everybody's too hungry to find the treasure!" said Seagull.

"Hungry pirates? Then it's pancake time!" said Pancake Polly.
---
Up came Pancake Polly with a great big pile of plates. Pancakes with banana, strawberries and honey for everyone!

The pirates ate and ate, and Seagull had a pancake too. "Yum, yum, yum!" said Seagull.
---
Now everyone's tummy was full, and they all felt happy and bouncy again! Izzy and Pancake Polly danced across the deck, and Billy sang from the top of the mast.

"Yo ho, yo ho!" sang Izzy.

"Yo ho, yo ho!" sang Billy.

"We're going to find the lost treasure!" sang Pancake Polly.
---
"Land ahoy! I can see an island!" said Billy, and he pointed far across the sea.

Everyone cheered. Kathryn looked through her telescope. "There it is! Treasure Island!" said Kathryn.
---
The ship stopped near the island, and Seagull flew on ahead to the sandy beach.

And there in the sand were two long sticks, crossed in a big X. "Squawk! Squawk! Squawk! I found it! X marks the treasure!" said Seagull.

Splish, splash, splosh! Behind her, the pirates came through the water with their spades.
---
Dig, dig, dig! The pirates dug and dug at the X. And up came an old wooden treasure chest!
---
Kathryn lifted the lid. Ooh, look! Gold coins, sparkly jewels and shiny pearls, enough for everyone!

"Yo ho, yo ho!" sang Izzy.

"Yo ho, yo ho!" sang Billy.

"We found the lost treasure!" sang Pancake Polly.
---
That evening, as the sun went down, the pirates had a treasure party on the beach, with music and dancing.

Kathryn gave Seagull a big, gentle hug. "Thank you, Seagull. You're the best friend a pirate could ever have!" said Kathryn.

"Squawk, squawk, squawk," said Seagull softly, and she snuggled down on Kathryn's big hat.
""";
}
